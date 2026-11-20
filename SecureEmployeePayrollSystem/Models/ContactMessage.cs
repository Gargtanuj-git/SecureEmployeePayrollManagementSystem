using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SecureEmployeePayrollSystem.Models;

[Table("CONTACT_MESSAGE")]
public class ContactMessage
{
    [Key]
    [Column("Message_ID")]
    public int Id { get; set; }

    [Required(ErrorMessage = "Full Name is required.")]
    [StringLength(100, ErrorMessage = "Full Name cannot exceed 100 characters.")]
    [Column("Full_Name")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Work Email is required.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    [StringLength(150, ErrorMessage = "Work Email cannot exceed 150 characters.")]
    [Column("Work_Email")]
    public string WorkEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "Subject is required.")]
    [StringLength(200, ErrorMessage = "Subject cannot exceed 200 characters.")]
    [Column("Subject")]
    public string Subject { get; set; } = string.Empty;

    [Required(ErrorMessage = "Message is required.")]
    [StringLength(2000, ErrorMessage = "Message cannot exceed 2000 characters.")]
    [Column("Message")]
    public string Message { get; set; } = string.Empty;

    [Column("Submitted_At_Utc")]
    public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;

    [StringLength(50)]
    [Column("Status")]
    public string Status { get; set; } = "New";

    [StringLength(50)]
    [Column("Ip_Address")]
    public string? IpAddress { get; set; }
}
