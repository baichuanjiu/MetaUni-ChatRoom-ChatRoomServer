using Minio;

namespace ChatRoom.API.MinIO
{
    public class MessageMediasMinIOService
    {
        private readonly MinioClient _client;
        private readonly IConfiguration _configuration;
        private readonly ILogger<MessageMediasMinIOService> _logger;

        public MessageMediasMinIOService(MinioClient client, IConfiguration configuration, ILogger<MessageMediasMinIOService> logger)
        {
            _client = client.Build();
            _configuration = configuration;
            _logger = logger;
            _ = SetMinIO();
        }

        public async Task SetMinIO()
        {
            BucketExistsArgs bucketExistsArgs = new BucketExistsArgs().WithBucket(_configuration["MinIO:MessageMediasBucketName"]!);
            if (await _client.BucketExistsAsync(bucketExistsArgs))
            {
            }
            else
            {
                MakeBucketArgs makeBucketArgs = new MakeBucketArgs().WithBucket(_configuration["MinIO:MessageMediasBucketName"]!);
                await _client.MakeBucketAsync(makeBucketArgs);
                string policyJson = _configuration["MinIO:MessageMediasBucketPolicyJSON"]!;
                SetPolicyArgs setPolicyArgs = new SetPolicyArgs().WithBucket(_configuration["MinIO:MessageMediasBucketName"]!).WithPolicy(policyJson);
                await _client.SetPolicyAsync(setPolicyArgs);
            }
        }

        public async Task<bool> UploadImageAsync(string imageName, Stream file)
        {
            try
            {
                PutObjectArgs putObjectArgs = new PutObjectArgs().WithBucket(_configuration["MinIO:MessageMediasBucketName"]!).WithObject(imageName).WithStreamData(file).WithObjectSize(file.Length)
                    .WithContentType("application/octet-stream");
                await _client.PutObjectAsync(putObjectArgs);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError("Error：MinIO存储图片时失败，桶名为[ {bucketName} ]，图片名为[ {imageName} ]，报错信息为[ {ex} ]。", _configuration["MinIO:MessageMediasBucketName"]!, imageName, ex);
                return false;
            }
        }

        public async Task<bool> UploadVideoAsync(string videoName, Stream file)
        {
            try
            {
                PutObjectArgs putObjectArgs = new PutObjectArgs().WithBucket(_configuration["MinIO:MessageMediasBucketName"]!).WithObject(videoName).WithStreamData(file).WithObjectSize(file.Length)
                    .WithContentType("application/octet-stream");
                await _client.PutObjectAsync(putObjectArgs);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError("Error：MinIO存储视频时失败，桶名为[ {bucketName} ]，视频名为[ {videoName} ]，报错信息为[ {ex} ]。", _configuration["MinIO:MessageMediasBucketName"]!, videoName, ex);
                return false;
            }
        }

        public async Task<bool> DeleteFilesAsync(List<string> paths)
        {
            try
            {
                RemoveObjectsArgs removeObjectsArgs = new RemoveObjectsArgs()
                    .WithBucket(_configuration["MinIO:MessageMediasBucketName"]!)
                    .WithObjects(paths);
                await _client.RemoveObjectsAsync(removeObjectsArgs);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError("Error：MinIO删除文件时失败，桶名为[ {bucketName} ]，删除的文件为[ {paths} ]，报错信息为[ {ex} ]。", _configuration["MinIO:MessageMediasBucketName"]!, paths, ex);
                return false;
            }
        }
    }
}
