namespace LanChat.Shared.DTOs
{
    /// <summary>
    /// Yêu cầu bắt đầu truyền file
    /// </summary>
    public class FileTransferInitiateRequest
    {
        /// <summary>Tên file</summary>
        public string FileName { get; set; } = string.Empty;
        
        /// <summary>Kích thước file (bytes)</summary>
        public long FileSize { get; set; }
        
        /// <summary>MD5 hash của file (để xác minh)</summary>
        public string FileHash { get; set; } = string.Empty;
        
        /// <summary>ID người nhận file</summary>
        public int ReceiverId { get; set; }
        
        /// <summary>ID truyền file (dùng để theo dõi)</summary>
        public string TransferId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Phản hồi yêu cầu truyền file
    /// </summary>
    public class FileTransferResponse
    {
        /// <summary>ID truyền file</summary>
        public string TransferId { get; set; } = string.Empty;
        
        /// <summary>Chấp nhận: true/false</summary>
        public bool Accepted { get; set; }
        
        /// <summary>Lý do từ chối (nếu có)</summary>
        public string? Reason { get; set; }
    }

    /// <summary>
    /// Dữ liệu file được gửi
    /// </summary>
    public class FileTransferDataChunk
    {
        /// <summary>ID truyền file</summary>
        public string TransferId { get; set; } = string.Empty;
        
        /// <summary>Số thứ tự chunk (0-indexed)</summary>
        public int ChunkIndex { get; set; }
        
        /// <summary>Dữ liệu chunk (base64 để dễ gửi qua JSON)</summary>
        public string ChunkData { get; set; } = string.Empty;
        
        /// <summary>Kích thước chunk thực tế (bytes)</summary>
        public int ChunkSize { get; set; }
        
        /// <summary>Đây có phải chunk cuối cùng?</summary>
        public bool IsLastChunk { get; set; }
    }

    /// <summary>
    /// Xác nhận nhận file chunk
    /// </summary>
    public class FileTransferAck
    {
        /// <summary>ID truyền file</summary>
        public string TransferId { get; set; } = string.Empty;
        
        /// <summary>Chunk cuối cùng được nhận</summary>
        public int LastChunkIndex { get; set; }
        
        /// <summary>Trạng thái: "ok" hoặc "error"</summary>
        public string Status { get; set; } = "ok";
    }
}
