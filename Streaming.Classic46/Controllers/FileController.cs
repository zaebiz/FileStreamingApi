using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Hosting;
using System.Web.Http;
using System.Web.Http.Results;
using NLog;
using NLog.Fluent;

namespace Streaming.Classic46.Controllers
{
    [RoutePrefix("api/file")]
    public class FileController : ApiController
    {
        private string MapPath(string fileName)
        {
            return HostingEnvironment.MapPath($"/file/{fileName}");
        }



        [HttpPost, Route("upload2")]
        public async Task<IHttpActionResult> UploadFileMemory(string fileId)
        {
            LogManager.GetCurrentClassLogger().Log(LogLevel.Info, $"received request {fileId}");

            if (!Request.Content.IsMimeMultipartContent())
            {
                return new BadRequestErrorMessageResult("некорректный mime-тип запроса. ожидается Multipart/FormData", this);
            }

            MultipartMemoryStreamProvider provider = null;
            try
            {
                provider = await Request.Content.ReadAsMultipartAsync();
                LogManager.GetCurrentClassLogger().Log(LogLevel.Info, $"finish reading parts to file {fileId}");
            }
            catch (Exception ex)
            {
                LogManager.GetCurrentClassLogger().Log(LogLevel.Error, ex);
            }

            if (provider?.Contents != null)
            {
                foreach (var content in provider.Contents)
                {

                    //var name = content.Headers.ContentDisposition.FileName.Replace("\"", ""); 
                    //name = fileId + Path.GetExtension(name);

                    LogManager.GetCurrentClassLogger().Log(LogLevel.Info, $"write to file :  {MapPath(fileId)}");
                    using (var fileStream = File.OpenWrite(MapPath(fileId)))
                    using (var contentStream = await content.ReadAsStreamAsync())
                    {
                        fileStream.Seek(0, SeekOrigin.End);
                        await contentStream.CopyToAsync(fileStream);
                        contentStream.Close();
                    }

                    var info = new FileInfo(MapPath(fileId));
                    if (info.Exists)
                    {
                        LogManager.GetCurrentClassLogger().Log(LogLevel.Info, $"success . current file size :  {info.Length} bytes");
                    }
                }
            }

            LogManager.GetCurrentClassLogger().Log(LogLevel.Info, $"finish request { fileId}");
            return Ok(fileId);
        }

        [HttpPost, Route("upload")]
        public async Task<IHttpActionResult> UploadFileDisk(string fileId)
        {
            LogManager.GetCurrentClassLogger().Log(LogLevel.Info, $"received request {fileId}");

            if (string.IsNullOrEmpty(fileId))
            {
                LogManager.GetCurrentClassLogger().Log(LogLevel.Error, $"не указан Id файла");
                return new BadRequestErrorMessageResult("не указан Id файла", this);
            }
            if (!Request.Content.IsMimeMultipartContent())
            {
                LogManager.GetCurrentClassLogger().Log(LogLevel.Error, $"некорректный mime-тип запроса. ожидается Multipart/FormData");
                return new BadRequestErrorMessageResult("некорректный mime-тип запроса. ожидается Multipart/FormData", this);
            }

            string root = HttpContext.Current.Server.MapPath("~/chunk");
            var provider = new MultipartFormDataStreamProvider(root);
            try
            {
                await Request.Content.ReadAsMultipartAsync(provider);
                LogManager.GetCurrentClassLogger().Log(LogLevel.Info, $"finish reading parts to file {fileId}");
            }
            catch (Exception ex)
            {
                LogManager.GetCurrentClassLogger().Log(LogLevel.Error, ex);
            }

            foreach (MultipartFileData file in provider.FileData)
            {
                LogManager.GetCurrentClassLogger().Log(LogLevel.Info, $"Save chunk :  {file.LocalFileName}");
                LogManager.GetCurrentClassLogger().Log(LogLevel.Info, $"write to file :  {MapPath(fileId)}");

                var info = new FileInfo(MapPath(fileId));
                var size = info.Exists ? info.Length : 0;
                LogManager.GetCurrentClassLogger().Log(LogLevel.Info, $"current file size : {size} bytes");
                    
                using (var chunkStream = File.OpenRead(file.LocalFileName))
                using (var fileStream = File.OpenWrite(MapPath(fileId)))
                {
                    fileStream.Seek(0, SeekOrigin.End);
                    await chunkStream.CopyToAsync(fileStream);
                }

                info = new FileInfo(MapPath(fileId));
                if (info.Exists)
                {
                    LogManager.GetCurrentClassLogger().Log(LogLevel.Info, $"success . current file size :  {info.Length} bytes");
                }
            }

            LogManager.GetCurrentClassLogger().Log(LogLevel.Info, $"finish request { fileId}");
            return Ok(fileId);
        }

        [HttpGet, Route("size")]
        public IHttpActionResult FileSize(string fileId)
        {
            var size = new FileInfo(MapPath(fileId)).Length;
            return Ok(size);
        }


        [HttpGet, Route("download")]
        public HttpResponseMessage DownloadFileAsStream(string fileId)
        {
            var fileStream = File.OpenRead(MapPath(fileId));
            var contentType = new MediaTypeHeaderValue("application/octet-stream");

            if (Request.Headers.Range != null)
            {
                if (Request.Headers.Range.Ranges.Count > 1)
                    return Request.CreateResponse(HttpStatusCode.RequestedRangeNotSatisfiable);

                try
                {
                    HttpResponseMessage partialResponse = Request.CreateResponse(HttpStatusCode.PartialContent);
                    partialResponse.Content = new ByteRangeStreamContent(fileStream, Request.Headers.Range, contentType);
                    partialResponse.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
                    {
                        FileName = fileId
                    };

                    return partialResponse;
                }
                catch
                {
                    return Request.CreateResponse(HttpStatusCode.RequestedRangeNotSatisfiable);
                }
            }
            else
            {
                var result = Request.CreateResponse(HttpStatusCode.OK);
                result.Content = new StreamContent(fileStream);
                result.Content.Headers.ContentType = contentType;
                result.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
                {
                    FileName = fileId
                };

                return result;
            }

        }
    }
}
