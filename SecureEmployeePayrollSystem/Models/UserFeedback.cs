using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SecureEmployeePayrollSystem.Models;

[Table("USER_FEEDBACK")]
public class UserFeedback
{
    [Key]
    [Column("Feedback_ID")]
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

    [Required(ErrorMessage = "Feedback category is required.")]
    [StringLength(50)]
    [Column("Category")]
    public string Category { get; set; } = "General Feedback";

    [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5.")]
    [Column("Rating")]
    public int Rating { get; set; } = 5;

    [Required(ErrorMessage = "Feedback message is required.")]
    [StringLength(2000, ErrorMessage = "Feedback message cannot exceed 2000 characters.")]
    [Column("Message")]
    public string Message { get; set; } = string.Empty;

    [Column("Submitted_At_Utc")]
    public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;

    [StringLength(50)]
    [Column("Status")]
    public string Status { get; set; } = "Submitted";

    [Column("Is_Public")]
    public bool IsPublic { get; set; } = false;
}
