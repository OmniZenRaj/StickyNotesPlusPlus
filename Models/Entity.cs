using System;
using System.Windows;
using System.Windows.Media;
using System.Data.SQLite;
using System.IO;
using System.Security.Principal;

namespace OmniZenNotes.Models
{
    using G = Utilities.Graphics;
    using EX = Utilities.Exceptions;

    public class Entity
    {
        // Identity Fields:
        public Guid ID { get; private set; }
        public string Name { get; private set; }
        public string Description { get; private set; }

        // Inheritance & Composition Fields
        public Guid SuperID { get; private set; }
        public Guid ParentID { get; private set; }

        // UX Fields:
        [Xceed.Wpf.Toolkit.PropertyGrid.Attributes.ExpandableObject]
        public UXSettings UXSettings { get; set; } = new UXSettings();

        // Audit Security Fields:
        [Xceed.Wpf.Toolkit.PropertyGrid.Attributes.ExpandableObject]
        public Security Security { get; set; } = new Security();

        public Entity(Guid guid, string name, string description) : this(guid) {
            ID = guid;
            Name = name;
            Description = description;
        }

        public Entity(Guid guid) : this() {
            ID = guid;
        }

        public Entity() {
        }

        protected void LoadFromSQL(SQLiteDataReader reader) {
            // Load common Entity fields:
            try {

                ID = GetGuid(reader, "ID");

                Name = GetString(reader, "Name");
                Description = GetString(reader, "Description");

                var settings = GetString(reader, "UXSettings");
                if (!string.IsNullOrEmpty(settings)) {
                    UXSettings = Utilities.Json.GetObjectFromJson<UXSettings>(settings);
                };

                Security.OwnerSID = GetString(reader, "OwnerSID");
                Security.Permissions = Enum.Parse<EntityPermissions>(GetString(reader, "Permissions") ?? "Private");
                Security.CreatedBy = GetString(reader, "CreatedBy");
                Security.CreatedDTS = GetDateTime(reader, "CreatedDTS");
                Security.UpdatedBy = GetString(reader, "UpdatedBy");
                Security.UpdatedDTS = GetDateTime(reader, "UpdatedDTS");
            } catch (Exception ex) {
                EX.LogException(ex, "Entity LoadFromSQL FAILED:");
            }
        }

        public void LoadToSQL(SQLiteCommand cmd) {
            try {
                cmd.Parameters.AddWithValue("@ID", ID.ToString());
                cmd.Parameters.AddWithValue("@Name", Name);
                cmd.Parameters.AddWithValue("@Description", Description);
                cmd.Parameters.AddWithValue("@UXSettings", Utilities.Json.GetJsonFromObject(UXSettings));
                cmd.Parameters.AddWithValue("@OwnerSID", Security.OwnerSID);
                cmd.Parameters.AddWithValue("@Permissions", Security.Permissions.ToString());
                cmd.Parameters.AddWithValue("@CreatedBy", Security.CreatedBy);
                cmd.Parameters.AddWithValue("@UpdatedBy", Security.UpdatedBy);
            } catch (Exception ex) {
                EX.LogException(ex, "Entity LoadToSQL FAILED:");
            }
        }

        protected string GetString(SQLiteDataReader reader, string colName) {
            int colIndex = reader.GetOrdinal(colName);
            return GetString(reader, colIndex);
        }

        protected string GetString(SQLiteDataReader reader, int colIndex) {
            return reader.IsDBNull(colIndex) ? null : reader.GetString(colIndex);
        }

        protected DateTime GetDateTime(SQLiteDataReader reader, string colName) {
            int colIndex = reader.GetOrdinal(colName);
            return reader.IsDBNull(colIndex) ? new DateTime() : reader.GetDateTime(colIndex);
        }

        protected decimal GetDecimal(SQLiteDataReader reader, string colName) {
            int colIndex = reader.GetOrdinal(colName);
            return reader.IsDBNull(colIndex) ? 0M : reader.GetDecimal(colIndex);
        }

        protected bool GetBoolean(SQLiteDataReader reader, string colName) {
            int colIndex = reader.GetOrdinal(colName);
            return reader.IsDBNull(colIndex) ? false : reader.GetBoolean(colIndex);
        }

        protected Guid GetGuid(SQLiteDataReader reader, string colName) {
            int colIndex = reader.GetOrdinal(colName);
            return reader.IsDBNull(colIndex) ? Guid.NewGuid() : Guid.Parse(reader.GetString(colIndex));
        }

        // We can't just use reader.GetBlob because our Table was created WITHOUT ROWID
        protected long GetBlob(SQLiteDataReader reader, int colIndex, MemoryStream ms) {
            long byteCount = reader.GetBytes(colIndex, 0, null, 0, 0);  // Get total byte count
            byte[] buffer = new byte[byteCount];
            long bytesRead = reader.GetBytes(colIndex, 0, buffer, 0, buffer.Length);
            ms.Write(buffer, 0, (int)bytesRead);
            return bytesRead;
        }
    }

    public class UXSettings
    {
        public Rect RestoreBounds { get; set; }
        public WindowState WindowState { get; set; }
        public FontFamily FontFamily { get ; set; } 
        public double FontSize { get; set; }
        public FontStyle FontStyle { get; set; }
        public Color FontColor { get; set; }
        public Color BackgroundColor { get; set; }
        public bool OptionsExpanded { get; set; } = false;
        public bool FormatBar { get; set; } = false;
        public bool SpellCheck { get; set; } = false;
        public bool Topmost { get; set; } = false;
        public Visibility Visibility { get; set; } = Visibility.Visible;
        public int ZOrder { get; set; } = 1;
        public double ToolBarScale { get; set; } = 1.0;
        public int MonitorNumber { get; set; }
        public string CSS { get; set; }

        public string FontName => G.GetFamilyFontName(FontFamily);
        public override string ToString() => $@"{G.GetFamilyFontName(FontFamily)} {FontSize:F0} {FontStyle}";

        public UXSettings() {
            RestoreBounds = Properties.Settings.Default.RestoreBounds;
            FontFamily = SystemFonts.MenuFontFamily;
            FontStyle = SystemFonts.MenuFontStyle;
            FontSize = Properties.Settings.Default.FontSize;
            FontColor = Properties.Settings.Default.FontColor;
            BackgroundColor = Properties.Settings.Default.BackgroundColor;
        }

        // RND: May need to do a deeper Clone to avoid duplicate references
        public void CloneFrom(UXSettings copy) {
            RestoreBounds = copy.RestoreBounds;
            FontFamily = copy.FontFamily;
            FontSize = copy.FontSize;
            FontStyle = copy.FontStyle;
            FontColor = copy.FontColor;
            BackgroundColor = copy.BackgroundColor;
            OptionsExpanded = copy.OptionsExpanded;
            FormatBar = copy.FormatBar;
            SpellCheck = copy.SpellCheck;
            Topmost = copy.Topmost;
            Visibility = copy.Visibility;
            ZOrder = copy.ZOrder;
            ToolBarScale = copy.ToolBarScale;
            MonitorNumber = copy.MonitorNumber;
            CSS = copy.CSS;
        }
    }

    public class Security
    {
        public string OwnerSID { get; internal set; } = WindowsIdentity.GetCurrent()?.User?.Value;
        public EntityPermissions Permissions { get; set; } = EntityPermissions.Private;
        public string CreatedBy { get; internal set; } = WindowsIdentity.GetCurrent()?.Name;
        public DateTime CreatedDTS { get; internal set; } = DateTime.Now;
        public string UpdatedBy { get; internal set; } = WindowsIdentity.GetCurrent()?.Name;
        public DateTime UpdatedDTS { get; internal set; } = DateTime.Now;
        
        public override string ToString() => $@"Created by {CreatedBy} on {CreatedDTS}"; // NLS:
    }

    public enum EntityPermissions { Read, Modify, Full, Private };
}
