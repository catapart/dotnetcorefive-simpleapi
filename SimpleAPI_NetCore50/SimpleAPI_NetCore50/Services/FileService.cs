using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using SimpleAPI_NetCore50.Websockets;

namespace SimpleAPI_NetCore50.Services
{

    // WARNING: This service saves files directly to your disk. This is necessary for streaming large files.
    // Any files that are uploaded should, ideally, be cached into a memory stream before ever saving to disk.
    // Some files are too large for memory, so a compromise is to save them to a Quarantined directory that is
    // monitored by your anitivirus solution (usually on an isolated drive), before being distributed elsewhere
    // in the filesystem. If you do not have a quarantined directory for the files to be uploaded to, you SHOULD
    // NOT use the file upload functionality here.
    public class FileService
    {
        // If you require a check on specific characters in the IsValidFileExtensionAndSignature
        // method, supply the characters in the _allowedChars field.
        private static readonly byte[] _allowedChars = { };

        // For more file signatures, see the File Signatures Database (https://www.filesignatures.net/)
        // and the official specifications for the file types you wish to add.
        private static readonly Dictionary<string, List<byte[]>> _fileSignature = new Dictionary<string, List<byte[]>>
        {
            { ".gif", new List<byte[]> { new byte[] { 0x47, 0x49, 0x46, 0x38 } } },
            { ".png", new List<byte[]> { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } } },
            { ".jpeg", new List<byte[]>
                {
                    new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 },
                    new byte[] { 0xFF, 0xD8, 0xFF, 0xE2 },
                    new byte[] { 0xFF, 0xD8, 0xFF, 0xE3 },
                }
            },
            { ".jpg", new List<byte[]>
                {
                    new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 },
                    new byte[] { 0xFF, 0xD8, 0xFF, 0xE1 },
                    new byte[] { 0xFF, 0xD8, 0xFF, 0xE8 },
                }
            },
            { ".zip", new List<byte[]>
                {
                    new byte[] { 0x50, 0x4B, 0x03, 0x04 },
                    new byte[] { 0x50, 0x4B, 0x4C, 0x49, 0x54, 0x45 },
                    new byte[] { 0x50, 0x4B, 0x53, 0x70, 0x58 },
                    new byte[] { 0x50, 0x4B, 0x05, 0x06 },
                    new byte[] { 0x50, 0x4B, 0x07, 0x08 },
                    new byte[] { 0x57, 0x69, 0x6E, 0x5A, 0x69, 0x70 },
                }
            },
        };

        public async Task<Models.FileMap> StreamFileToDiskWithProgress(HttpRequest request, ModelStateDictionary modelState, ILogger logger, ProgressSocketSessionService sessionService, string sessionKey)
        {
            if(!modelState.IsValid)
            {
                return null;
            }

            if (!IsMultipartContentType(request.ContentType))
            {
                modelState.AddModelError("File", "Form content type must be 'multipart'");
                return null;
            }

            var boundary = GetBoundary(MediaTypeHeaderValue.Parse(request.ContentType), sessionService.DefaultFormOptions.MultipartBoundaryLengthLimit);
            var reader = new MultipartReader(boundary, request.Body);
            var section = await reader.ReadNextSectionAsync();

            string filenameForDisplay = "";
            string filenameOnDisk = "";

            while (section != null)
            {
                var hasContentDispositionHeader = ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var contentDisposition);

                if (hasContentDispositionHeader)
                {
                    // This check assumes that there's a file
                    // present without form data. If form data
                    // is present, this method immediately fails
                    // and returns the model error.
                    if (!HasFileContentDisposition(contentDisposition))
                    {
                        modelState.AddModelError("File", $"The request couldn't be processed (Error 2).");
                        return null;
                    }
                    else
                    {
                        // Don't trust the file name sent by the client. To display
                        // the file name, HTML-encode the value.
                        filenameForDisplay = WebUtility.HtmlEncode(contentDisposition.FileName.Value);
                        filenameOnDisk = Path.GetRandomFileName();

                        if (!Directory.Exists(sessionService.QuarantinedFilePath))
                        {
                            Directory.CreateDirectory(sessionService.QuarantinedFilePath);
                        }
                        string quarantinedPath = Path.Combine(sessionService.QuarantinedFilePath, filenameOnDisk);

                        SocketSession targetSession = sessionService.GetSessionByKey(sessionKey);
                        if(targetSession == null)
                        {
                            modelState.AddModelError("File", "The requested session could not be found.");
                            return null;
                        }

                        int totalBytes = targetSession.GetAttributeValue<int>("unitTotal");

                        await SaveFileToDiskWithProgress(section, contentDisposition, modelState, quarantinedPath, sessionService.PermittedExtensions, sessionService.FileSizeLimit, sessionService, targetSession, totalBytes);

                        if (!modelState.IsValid)
                        {
                            return null;
                        }

                        if (!Directory.Exists(sessionService.FileStoragePath))
                        {
                            Directory.CreateDirectory(sessionService.FileStoragePath);
                        }

                        System.IO.File.Move(quarantinedPath, Path.Combine(sessionService.FileStoragePath, filenameOnDisk));
                        logger.LogInformation("Uploaded file '{TrustedFileNameForDisplay}' saved to '{TargetFilePath}' as {TrustedFileNameForFileStorage}",
                                filenameForDisplay,
                                sessionService.FileStoragePath,
                                filenameOnDisk);
                    }
                }

                // Drain any remaining section body that hasn't been consumed and
                // read the headers for the next section.
                section = await reader.ReadNextSectionAsync();
            }

            return new Models.FileMap() { FilenameForDisplay = filenameForDisplay, FilenameOnDisk = filenameOnDisk };
        }

        public async Task SaveFileToDiskWithProgress(MultipartSection section, ContentDispositionHeaderValue contentDisposition, ModelStateDictionary modelState, string filepath, string[] permittedExtensions, long sizeLimit, ProgressSocketSessionService sessionService, SocketSession targetSession, int totalBytes)
        {
            using (FileStream stream = new FileStream(filepath, FileMode.Create, System.IO.FileAccess.Write))
            {
                // this is useful for guarding with smaller files; larger files that require streaming directly to disk can't validate;
                //if (!IsValidFileExtensionAndSignature(contentDisposition.FileName.Value, stream, permittedExtensions))
                //{
                //    modelState.AddModelError("File", "The file type isn't permitted or the file's signature doesn't match the file's extension.");
                //}

                await CopyToStreamWithProgress(section.Body, stream, sessionService, targetSession, totalBytes);

                if (stream.Length == 0)
                {
                    modelState.AddModelError("File", "The file is empty.");
                }
                else if (stream.Length > sizeLimit)
                {
                    var megabyteSizeLimit = sizeLimit / 1048576;
                    modelState.AddModelError("File", $"The file exceeds {megabyteSizeLimit:N1} MB.");
                }
            }
        }

        private async Task CopyToStreamWithProgress(Stream source, Stream destination, ProgressSocketSessionService sessionService, SocketSession targetSession, int totalBytes = -1, string stepId = "", CancellationToken cancellationToken = default(CancellationToken), int bufferSize = 0x1000)
        {
            byte[] buffer = new byte[bufferSize];
            int currentBytesRead;
            int totalBytesRead = 0;
            while((currentBytesRead = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                await destination.WriteAsync(buffer, 0, currentBytesRead, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                totalBytesRead += currentBytesRead;
                sessionService.UpdateProgress(targetSession.SessionKey, totalBytesRead, totalBytes, stepId);

                await Task.Delay(1); // Stops worker threads from going crazy with CPU
            }
        }

        private bool IsValidFileExtensionAndSignature(string fileName, Stream data, string[] permittedExtensions)
        {
            if (string.IsNullOrEmpty(fileName) || data == null || data.Length == 0)
            {
                return false;
            }

            var ext = Path.GetExtension(fileName).ToLowerInvariant();

            if (string.IsNullOrEmpty(ext) || !permittedExtensions.Contains(ext))
            {
                return false;
            }

            data.Position = 0;

            using (var reader = new BinaryReader(data))
            {
                if (ext.Equals(".txt") || ext.Equals(".csv") || ext.Equals(".prn"))
                {
                    if (_allowedChars.Length == 0)
                    {
                        // Limits characters to ASCII encoding.
                        for (var i = 0; i < data.Length; i++)
                        {
                            if (reader.ReadByte() > sbyte.MaxValue)
                            {
                                return false;
                            }
                        }
                    }
                    else
                    {
                        // Limits characters to ASCII encoding and
                        // values of the _allowedChars array.
                        for (var i = 0; i < data.Length; i++)
                        {
                            var b = reader.ReadByte();
                            if (b > sbyte.MaxValue ||
                                !_allowedChars.Contains(b))
                            {
                                return false;
                            }
                        }
                    }

                    return true;
                }

                // Uncomment the following code block if you must permit
                // files whose signature isn't provided in the _fileSignature
                // dictionary. We recommend that you add file signatures
                // for files (when possible) for all file types you intend
                // to allow on the system and perform the file signature
                // check.
                /*
                if (!_fileSignature.ContainsKey(ext))
                {
                    return true;
                }
                */

                // File signature check
                // --------------------
                // With the file signatures provided in the _fileSignature
                // dictionary, the following code tests the input content's
                // file signature.
                var signatures = _fileSignature[ext];
                var headerBytes = reader.ReadBytes(signatures.Max(m => m.Length));

                return signatures.Any(signature =>
                    headerBytes.Take(signature.Length).SequenceEqual(signature));
            }
        }

        private static bool IsMultipartContentType(string contentType)
        {
            return !string.IsNullOrEmpty(contentType) && contentType.IndexOf("multipart/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // Content-Type: multipart/form-data; boundary="----WebKitFormBoundarymx2fSWqWSd0OxQqq"
        // The spec at https://tools.ietf.org/html/rfc2046#section-5.1 states that 70 characters is a reasonable limit.
        private static string GetBoundary(MediaTypeHeaderValue contentType, int lengthLimit)
        {
            var boundary = HeaderUtilities.RemoveQuotes(contentType.Boundary).Value;

            if (string.IsNullOrWhiteSpace(boundary))
            {
                throw new InvalidDataException("Missing content-type boundary.");
            }

            if (boundary.Length > lengthLimit)
            {
                throw new InvalidDataException($"Multipart boundary length limit {lengthLimit} exceeded.");
            }

            return boundary;
        }
        private static bool HasFileContentDisposition(ContentDispositionHeaderValue contentDisposition)
        {
            // Content-Disposition: form-data; name="myfile1"; filename="Misc 002.jpg"
            return contentDisposition != null
                && contentDisposition.DispositionType.Equals("form-data")
                && (!string.IsNullOrEmpty(contentDisposition.FileName.Value)
                    || !string.IsNullOrEmpty(contentDisposition.FileNameStar.Value));
        }

        private static Encoding GetEncoding(MultipartSection section)
        {
            var hasMediaTypeHeader = MediaTypeHeaderValue.TryParse(section.ContentType, out var mediaType);

            // UTF-7 is insecure and shouldn't be honored. UTF-8 succeeds in 
            // most cases.
            if (!hasMediaTypeHeader || Encoding.UTF7.Equals(mediaType.Encoding))
            {
                return Encoding.UTF8;
            }

            return mediaType.Encoding;
        }
    }
}
