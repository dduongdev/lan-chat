using System;

namespace LanChat.Shared.Payloads
{
    /// <summary>
    /// Client yêu cầu quyền tải một tệp tin đã có trên Server.
    /// RoutingKey: file.download.req
    /// </summary>
    public class FileDownloadRequestPayload
    {
        /// <summary>ID của tệp tin muốn tải về.</summary>
        public Guid FileId { get; set; }
    }
}
