﻿using System.ComponentModel.DataAnnotations.Schema;

namespace was.api.Models.Dtos
{
    [Table("user_login")]
    public class DtoUser
    {
        [Column("id")]
        public int Id { get; set; }
        [Column("emp_id")]
        public string EmployeeId { get; set; }
        [Column("f_name")]
        public string FirstName { get; set; }
        [Column("l_name")]
        public string LastName { get; set; }
        [Column("email")]
        public string Email { get; set; }
        [Column("mobile")] 
        public string? Mobile { get; set; }
        [Column("password")]
        public string Password { get; set; }
        [Column("password_otp")]
        public string? PasswordOtp { get; set; }
        [Column("otp_created_at")]
        public DateTime? OtpCreatedAt { get; set; }
        [Column("refresh_token")]
        public string? RefreshToken { get; set; }
        [Column("role_id")]
        public int RoleId { get; set; }

        [Column("active_status")]
        /// <summary>
        /// 0:Deactivated, 1:Active, 2:Blocked
        /// </summary>
        public int ActiveStatus { get; set; }
        [Column("created_by")]
        public string CreatedBy { get; set; }
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
        [Column("updated_by")]
        public int? UpdatedBy { get; set; }
        [Column("updated_date")]
        public DateTime? UpdatedDate { get; set; }
        [Column("facility_zone_location")]
        public string FacilityZoneLocation { get; set; }
        [Column("zone")]
        public string Zone { get; set; }
        [Column("facility")]
        public string Facility { get; set; }
    }
}
