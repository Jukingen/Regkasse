namespace KasseAPI_Final.Tse
{
    /// <summary>
    /// RKSV SignaturePipeline hata kodları: CMC_MISSING_KEY, CERT_MISMATCH, INVALID_SIGNATURE_FORMAT, BASE64URL_PADDING_ERROR
    /// </summary>
    public class TsePipelineException : Exception
    {
        public string ErrorCode { get; }

        public TsePipelineException(string errorCode, string message) : base(message)
        {
            ErrorCode = errorCode;
        }

        public TsePipelineException(string errorCode, string message, Exception inner) : base(message, inner)
        {
            ErrorCode = errorCode;
        }
    }
}
