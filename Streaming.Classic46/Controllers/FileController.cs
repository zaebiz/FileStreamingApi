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
            var logStream = File.OpenWrite(MapPath("log.txt"));
            logStream.Seek(0, SeekOrigin.End);

            var logWriter = new StreamWriter(logStream);
            logWriter.WriteLine($"received request {fileId} :  {DateTime.Now.ToLongTimeString()}");

            if (string.IsNullOrEmpty(fileId))
            {
                return new BadRequestErrorMessageResult("не указан Id файла", this);
            }
            if (!Request.Content.IsMimeMultipartContent())
            {
                return new BadRequestErrorMessageResult("некорректный mime-тип запроса. ожидается Multipart/FormData", this);
            }


            MultipartMemoryStreamProvider data = null;
            try
            {
                data = await Request.Content.ReadAsMultipartAsync();
            }
            catch (Exception ex)
            {
                throw ex;
            }

            foreach (var content in data.Contents)
            {
                logWriter.WriteLine($"start handle chunk {fileId} :  {DateTime.Now.ToLongTimeString()}");
               
                //var name = content.Headers.ContentDisposition.FileName.Replace("\"", ""); 
                //name = fileId + Path.GetExtension(name);

                using (var fileStream = File.OpenWrite(MapPath(fileId)))
                {
                    fileStream.Seek(0, SeekOrigin.End);
                    var contentStream = await content.ReadAsStreamAsync();
                    await contentStream.CopyToAsync(fileStream);
                    contentStream.Close();
                    logWriter.WriteLine($"finish handle chunk {fileId} :  {DateTime.Now.ToLongTimeString()}");
                }
            }


            logWriter.Close();
            logStream.Close();

            return Ok(fileId);
        }

        [HttpPost, Route("upload")]
        public async Task<IHttpActionResult> UploadFileDisk(string fileId)
        {
            var logStream = File.OpenWrite(MapPath("log.txt"));
            logStream.Seek(0, SeekOrigin.End);

            var logWriter = new StreamWriter(logStream);
            logWriter.WriteLine($"received request {fileId} :  {DateTime.Now.ToLongTimeString()}");

            if (string.IsNullOrEmpty(fileId))
            {
                return new BadRequestErrorMessageResult("не указан Id файла", this);
            }
            if (!Request.Content.IsMimeMultipartContent())
            {
                return new BadRequestErrorMessageResult("некорректный mime-тип запроса. ожидается Multipart/FormData", this);
            }


            string root = HttpContext.Current.Server.MapPath("~/chunk");
            var provider = new MultipartFormDataStreamProvider(root);
            try
            {
                await Request.Content.ReadAsMultipartAsync(provider);
                logWriter.WriteLine($"finish reading parts to file {fileId} :  {DateTime.Now.ToLongTimeString()}");
            }
            catch (Exception ex)
            {
                throw ex;
            }

            foreach (MultipartFileData file in provider.FileData)
            {
                continue;
            }


            //    foreach (var content in data.Contents)
            //{
            //    logWriter.WriteLine($"start handle chunk {fileId} :  {DateTime.Now.ToLongTimeString()}");

            //    //var name = content.Headers.ContentDisposition.FileName.Replace("\"", ""); 
            //    //name = fileId + Path.GetExtension(name);

            //    using (var fileStream = File.OpenWrite(MapPath(fileId)))
            //    {
            //        fileStream.Seek(0, SeekOrigin.End);
            //        var contentStream = await content.ReadAsStreamAsync();
            //        await contentStream.CopyToAsync(fileStream);
            //        contentStream.Close();
            //        logWriter.WriteLine($"finish handle chunk {fileId} :  {DateTime.Now.ToLongTimeString()}");
            //    }
            //}


            logWriter.WriteLine($"finish request {fileId} :  {DateTime.Now.ToLongTimeString()}");
            logWriter.Close();
            logStream.Close();

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
