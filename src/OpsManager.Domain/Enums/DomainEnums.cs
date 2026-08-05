namespace OpsManager.Domain.Enums;

public enum OrganizationStatus { Active, Suspended, Archived }
public enum UserAccountStatus { Active, Suspended, Disabled }
public enum OrganizationRole { Manager, Supervisor, Employee }
public enum EvidenceMode { None, Optional, Required }
public enum TaskPriority { Low, Normal, High, Urgent }
public enum OperationalTaskStatus
{
    NotStarted,
    InProgress,
    Blocked,
    PendingApproval,
    Returned,
    Completed,
    Cancelled,
}
public enum TaskItemStatus { Pending, Completed, Skipped }
public enum TaskItemType { Question, RatingSlider, SingleLineText, MultiLineText, MultipleChoice, Instruction }
public enum TaskAssignmentMode { SingleUser, SelectedUsers, AllDepartmentMembers }
public enum TaskExecutionWindowState { NotOpen, Open, Expired }
public enum TaskTemporalScope { Upcoming = 1, Past = 2 }
public enum RecurrenceType { Daily, Weekly, Monthly, SpecificDates }
public enum Weekday { Sunday, Monday, Tuesday, Wednesday, Thursday, Friday, Saturday }
public enum AttachmentType { Evidence, Reference, General }
public enum DepartmentOrderStatus { Draft, Submitted, Accepted, Preparing, Ready, Delivered, Received, Rejected, Cancelled }
public enum DepartmentOrderItemStatus { Pending, Preparing, Ready, Fulfilled, PartiallyFulfilled, Rejected }
public enum UnitCode { Each, Kilogram, Gram, Liter, Milliliter, Meter, Centimeter, Box, Package, Custom }
public enum ComplaintStatus { Submitted, UnderReview, InProgress, Resolved, Closed, Rejected }
public enum ComplaintVisibility { ManagementOnly, Participants }
public enum SubscriptionStatus { Trial, Active, GracePeriod, Expired, Suspended, Cancelled, Complimentary }
public enum BillingMode { Trial, Monthly, Yearly, Manual }
public enum SubscriptionActionType { Created, TrialStarted, Activated, Extended, Suspended, Reactivated, Expired, Cancelled }
public enum PaymentMethod { Cash, BankTransfer, CardTerminal, Other }
public enum PaymentStatus { Pending, Confirmed, Rejected, Refunded }
public enum PlatformRole { Administrator, Support }
public enum NotificationType { TaskAssigned, TaskDue, OrderUpdated, ComplaintUpdated, SubscriptionUpdated, System }
