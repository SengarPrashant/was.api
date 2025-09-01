namespace was.api.Helpers
{
    public class Constants
    {
        public enum UserStatus
        {
            Deactivated = 0,
            Active = 1,
            Blocked = 2
        }
        public enum Roles
        {
            Admin = 1,
            ProjectManager_FacilityManager = 2,
            AreaManager = 3,
            EHSManager = 4
        }
        public enum FormStatus
        {
            Pending = 0,
            Approved = 1,
            Work_in_progress = 2,
            Closed = 3,
        }
    }
    public static class OptionTypes
    {
        public static readonly string form_status = "form_status";
        public static readonly string facility_zone_location = "facility_zone_location";
        public static readonly string zone = "zone";
        public static readonly string zone_facility = "zone_facility";
        public const string work_permit = "work_permit";
        public const string incident = "incident";
    }
}

