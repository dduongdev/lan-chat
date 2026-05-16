using System;

namespace LanChat.Server.Entities
{
    /// <summary>
    /// Lưu trữ metadata của mỗi tệp tin được tải lên Server (Store & Forward).
    /// </summary>
    public sealed class FileTransfer
    {
        /// <summary>Khóa chính, định danh duy nhất cho mỗi tệp tin.</summary>
        public Guid Id { get; set; }

        /// <summary>Khóa ngoại tới User.Id, định danh người tải lên.</summary>
        public Guid UploaderId { get; set; }

        /// <summary>Tên tệp tin gốc do người dùng cung cấp.</summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>Kích thước tệp tin (bytes).</summary>
        public long FileSize { get; set; }

        /// <summary>SHA-256 Hash của tệp tin gốc, dùng để kiểm tra toàn vẹn.</summary>
        public string FileHash { get; set; } = string.Empty;

        /// <summary>Tên tệp tin vật lý trên đĩa Server (sử dụng Id để tránh xung đột).</summary>
        public string StoragePath { get; set; } = string.Empty;

        /// <summary>Trạng thái: "Uploading", "Available", "Corrupted".</summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>Thời gian bắt đầu yêu cầu upload.</summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>"PRIVATE" hoặc "GROUP" - loại đích gửi.</summary>
        public string TargetType { get; set; } = string.Empty;

        /// <summary>Username (PRIVATE) hoặc GroupId (GROUP) - đích gửi.</summary>
        public string TargetId { get; set; } = string.Empty;

        // Navigation properties
        public User Uploader { get; set; } = null!;
    }
}
