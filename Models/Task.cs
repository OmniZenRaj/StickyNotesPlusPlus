using System;
using System.Data.SQLite;

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

namespace OmniZenNotes.Models
{
    using EX = Utilities.Exceptions;

    public enum TaskStatus { New, InProgress, Completed, Waiting, Deferred };
    public enum TaskPriority { Low, Normal, High, Urgent, Immediate };

    public class Task : Entity
    {
        [Xceed.Wpf.Toolkit.PropertyGrid.Attributes.ExpandableObject]
        public TaskTodo Todo { get; set; } = new TaskTodo();
        [Xceed.Wpf.Toolkit.PropertyGrid.Attributes.ExpandableObject]
        public TaskReminder Reminder { get; set; } = new TaskReminder();

        public Task(Guid guid) : base(guid) {
            Todo.StartDTS = DateTime.Now;
            Todo.DueDTS = Todo.StartDTS.AddDays(2);
            Reminder.ReminderDTS = Todo.StartDTS.AddDays(1);
        }

        public Task() : this(Guid.NewGuid()) {
        }

        public async void Save(bool saveAsync = true) {
            if (saveAsync) {
                await Repository.SaveTask(this);
            } else {
                Repository.SaveTask(this);
            }
        }

        public new void LoadFromSQL(SQLiteDataReader reader) {
            base.LoadFromSQL(reader);

            try {
                Todo.Subject = GetString(reader, "Subject");
                Todo.Status = Enum.Parse<TaskStatus>(GetString(reader, "Status") ?? " ");
                Todo.Priority = Enum.Parse<TaskPriority>(GetString(reader, "Priority") ?? " ");
                Todo.StartDTS = GetDateTime(reader, "StartDTS");
                Todo.DueDTS = GetDateTime(reader, "DueDTS");
                Todo.CompletedDTS = GetDateTime(reader, "CompletedDTS");
                Todo.TotalWork = GetDecimal(reader, "TotalWork");
                Todo.ActualWork = GetDecimal(reader, "ActualWork");

                var reminder = GetString(reader, "Reminder");
                if (!string.IsNullOrEmpty(reminder)) {
                    Reminder = Utilities.Json.GetObjectFromJson<TaskReminder>(reminder);
                };

            } catch (Exception ex) {
                EX.LogException(ex, "Task LoadFromSQL FAILED:");
            }
        }

        public new void LoadToSQL(SQLiteCommand cmd) {
            base.LoadToSQL(cmd);

            try {
                cmd.Parameters.AddWithValue("@Subject", Todo.Subject);
                cmd.Parameters.AddWithValue("@Status", Todo.Status.ToString());
                cmd.Parameters.AddWithValue("@Priority", Todo.Priority.ToString());
                cmd.Parameters.AddWithValue("@StartDTS", Todo.StartDTS);
                cmd.Parameters.AddWithValue("@DueDTS", Todo.DueDTS);
                cmd.Parameters.AddWithValue("@CompletedDTS", Todo.CompletedDTS);
                cmd.Parameters.AddWithValue("@TotalWork", Todo.TotalWork);
                cmd.Parameters.AddWithValue("@ActualWork", Todo.ActualWork);
                cmd.Parameters.AddWithValue("@Reminder", Utilities.Json.GetJsonFromObject(Reminder));
            } catch (Exception ex) {
                EX.LogException(ex, "Task LoadToSQL FAILED:");
            }
        }

        public override string ToString() => $"{Todo} : {Reminder}"; // NLS:

    }

    public enum TaskAlarmSound { IM, Mail, Reminder, SMS, LoopingAlarm1, LoopingAlarm2, LoopingCall, LoopingCall2 };

    public class TaskReminder
    {
        public bool ReminderOn { get; set; }
        public string ReminderMessage { get; set; }
        public DateTime ReminderDTS { get; set; }
        public TaskAlarmSound ReminderSound { get; set; } = TaskAlarmSound.Reminder;
        public string ReminderImageURI { get; set; }
        public bool LongNotification { get; set; }
        public uint SnoozeCount { get; set; }
        public uint SnoozeInterval { get; set; }
        public string Recurrrence { get; set; }
        public override string ToString() => string.IsNullOrEmpty(ReminderMessage) ? ReminderOn ? "Remind Me" + $" on {ReminderDTS}" : "No Reminder Set" : ReminderMessage; // NLS:
    }

    public class TaskTodo
    {
        public string Subject { get; set; }
        public TaskStatus Status { get; set; }
        public TaskPriority Priority { get; set; }
        public DateTime StartDTS { get; set; }
        public DateTime DueDTS { get; set; }
        public DateTime CompletedDTS { get; set; }
        public decimal TotalWork { get; set; }
        public decimal ActualWork { get; set; }
        public override string ToString() =>  $"{Status} Task " + (CompletedDTS != DateTime.MinValue ? $"Completed on {CompletedDTS}" : $"Due On {DueDTS}") + $" {Priority} Priorty" ; // NLS:
    }
}
