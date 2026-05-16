using System;

namespace LanChat.Shared.Payloads
{
    /// <summary>
    /// Client yêu cầu quyền tải tệp tin lên Server.
    /// RoutingKey: file.upload.req
    /// </summary>
    public class FileUploadRequestPayload
    {
        /// <summary>"PRIVATE" hoặc "GROUP".</summary>
        public string TargetType { get; set; } = string.Empty;

        /// <summary>Username (PRIVATE) hoặc GroupId (GROUP).</summary>
        public string TargetId { get; set; } = string.Empty;

        /// <summary>Tên tệp tin.</summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>Kích thước tệp tin (bytes).</summary>
        public long FileSize { get; set; }

        /// <summary>SHA-256 Hash của tệp tin gốc.</summary>
        public string FileHash { get; set; } = string.Empty;

        /// <summary>UUID do Client tạo để định danh yêu cầu, tránh race condition khi gửi nhiều file liên tiếp.</summary>
        public Guid ClientRequestId { get; set; }
    }
}
