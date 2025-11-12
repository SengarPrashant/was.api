using Microsoft.EntityFrameworkCore;
using was.api.Helpers;
using was.api.Models;
using was.api.Models.Forms;
using was.api.Services.Coms;


namespace was.api.Services
{
    public class ReminderEmailScheduler : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private ILogger<ReminderEmailScheduler> _logger;

        public ReminderEmailScheduler(IServiceProvider serviceProvider, ILogger<ReminderEmailScheduler> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Reminder Email Scheduler started.");
            
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var _db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    // Query pending reminders
                    var sql = @"
                            SELECT DISTINCT 
                                f.id, 
                                f.form_id, 
                                (jsonb_extract_path_text(f.form_data, 'formDetails', 'datetime_of_work_to'))::timestamp AS datetime_of_work_to,
                                f.status, 
                                f.pending_with, 
                                f.zone_facility,
                                f.zone, 
                                f.facility_zone_location, 
                                f.submitted_date, 
                                f.submitted_by,
                                fd.title, 
                                fd.desc AS desc, 
                                fd.form_type, 
                                fd.form_type_key,
                                f.reminder_sent
                            FROM form_submissions f
                            INNER JOIN form_def fd ON f.form_id::text = fd.form_type_key
                            WHERE f.submitted_date >= (NOW() AT TIME ZONE 'UTC' - interval '1 year')
                               AND f.submitted_date <= (NOW() AT TIME ZONE 'UTC')
                              AND fd.form_type = {0}
                              AND (f.status = {1} OR f.status = {2})
                              AND f.reminder_sent = 0
                              AND (
                                (jsonb_extract_path_text(f.form_data, 'formDetails', 'datetime_of_work_to'))::timestamptz
                                BETWEEN NOW() 
                                AND (NOW() + interval '4 minute')
                              )
                            ";


                    var query = _db.WPEmailReminderResult.FromSqlRaw(sql,
                                                                       OptionTypes.work_permit,
                                                                       Convert.ToString((int)Constants.FormStatus.Approved),
                                                                       Convert.ToString((int)Constants.FormStatus.Work_in_progress))
                                .Select(f => new
                                {
                                    f.Id,
                                    FormTitle = f.Title,
                                    f.SubmittedDate,
                                    SubmittedBy = new KeyVal
                                    {
                                        key = f.SubmittedBy.ToString(),
                                        Value = _db.Users
                                         .Where(u => u.Id == f.SubmittedBy)
                                         .Select(u => u.FirstName + " " + u.LastName + "|" + u.Email)
                                         .FirstOrDefault()
                                    },
                                    ZoneFacility = new KeyVal
                                    {
                                        key = f.ZoneFacility,
                                        Value = _db.FormOptions
                                         .Where(o => o.OptionKey == f.ZoneFacility && o.OptionType == OptionTypes.zone_facility
                                         && o.CascadeType == OptionTypes.zone && o.CascadeKey == f.Zone)
                                         .Select(o => o.OptionValue)
                                         .FirstOrDefault()
                                    },
                            });

                    var results = await query.Distinct().OrderBy(f => f.SubmittedDate).ToListAsync();

                    if (!results.Any())
                    {
                        await Task.Delay(TimeSpan.FromMinutes(4), stoppingToken); // wait if no reminders
                        continue;
                    }

                    // Batch settings
                    int batchSize = 30;       // number of emails to send in parallel
                    int batchDelayMs = 4000;  // wait between batches to respect rate limits

                    for (int i = 0; i < results.Count; i += batchSize)
                    {

                        var batch = results.Skip(i).Take(batchSize)
                                .Select(async result =>
                                {
                                    using var innerScope = _serviceProvider.CreateScope();
                                    var innerDb = innerScope.ServiceProvider.GetRequiredService<AppDbContext>();
                                    var emailService = innerScope.ServiceProvider.GetRequiredService<IEmailService>();

                                    var SubmittedByParts = result.SubmittedBy.Value.Split("|");
                                    var toEmail = SubmittedByParts[1];
                                    var placeholders = new Dictionary<string, string>
                                    {
                                        { "WorkPermitName", result.FormTitle },
                                        { "FacilityName", result.ZoneFacility.Value },
                                        { "DateTime", result.SubmittedDate.ToISTString() },
                                        { "Requester", SubmittedByParts[0] }
                                    };

                                    var subject = $"{result.FormTitle} Closure reminder!";
                                    var templateName = "FM_CLOSER_REMINDER";

                                    await FireEmailReminder(emailService, innerDb, result.Id, toEmail, subject, templateName, placeholders);
                                });

                        await Task.WhenAll(batch);

                        if (i + batchSize < results.Count)
                        {
                            await Task.Delay(batchDelayMs, stoppingToken); // wait between batches
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in ReminderEmailScheduler");
                }

                // Wait 1 minute before next cycle
                await Task.Delay(TimeSpan.FromMinutes(4), stoppingToken);
            }
        }

        private async Task FireEmailReminder(IEmailService emailService, AppDbContext _db, long submissionId, string to, string subject, string template, Dictionary<string, string> placeholders)
        {
            try
            {
                await emailService.SendTemplatedEmailAsync(to, subject, template, placeholders);
               
                await _db.FormSubmissions
                    .Where(s => s.Id == submissionId)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.ReminderSent, 1));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,$"Failed to send reminder email to:{to}, subject:{subject}, placeholders:{placeholders.ToJsonString()}");
            }
        }
    }
}
