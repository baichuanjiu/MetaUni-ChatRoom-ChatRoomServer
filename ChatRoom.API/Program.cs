using ChatRoom.API;
using ChatRoom.API.MinIO;
using ChatRoom.API.Protos.Authentication;
using ChatRoom.API.Protos.ChatRequest;
using ChatRoom.API.Redis;
using ChatRoom.API.ServiceDiscover;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Minio.AspNetCore;
using Serilog;
using static ChatRoom.API.Filters.JWTAuthFilter;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

//配置请求体最大容量
builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = int.MaxValue;
});

//配置接收Form最大长度
builder.Services.Configure<FormOptions>(option => {
    option.MultipartBodyLengthLimit = int.MaxValue;
});

//配置Serilog
var configuration = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("appsettings.json").Build();
Log.Logger = new LoggerConfiguration().ReadFrom.Configuration(configuration).CreateLogger();
builder.Host.UseSerilog();

//配置Redis
builder.Services.AddSingleton<RedisConnection>();

//配置MinIO
builder.Services.AddMinio(options =>
{
    options.Endpoint = builder.Configuration["MinIO:Endpoint"]!;
    options.AccessKey = builder.Configuration["MinIO:AccessKey"]!;
    options.SecretKey = builder.Configuration["MinIO:SecretKey"]!;
});
builder.Services.AddSingleton<UserAvatarMinIOService>();
builder.Services.AddSingleton<MessageMediasMinIOService>();

//配置服务发现
builder.Services.AddSingleton<IServiceDiscover, ServiceDiscover>();

//配置gRPC
builder.Services
    .AddGrpcClient<Authenticate.AuthenticateClient>((serviceProvider, options) =>
    {
        var discover = serviceProvider.GetRequiredService<IServiceDiscover>();
        string address = discover.GetService(builder.Configuration["ServiceDiscover:ServiceName:Auth"]!);
        options.Address = new Uri(address);
    })
    .AddCallCredentials((context, metadata) =>
    {
        metadata.Add("id", builder.Configuration["RPCHeader:ID"]!);
        metadata.Add("jwt", builder.Configuration["RPCHeader:JWT"]!);
        return Task.CompletedTask;
    })
    .ConfigureChannel(options => options.UnsafeUseInsecureChannelCallCredentials = true)
    ;

builder.Services
    .AddGrpcClient<SendChatRequest.SendChatRequestClient>((serviceProvider, options) =>
    {
        var discover = serviceProvider.GetRequiredService<IServiceDiscover>();
        string address = discover.GetService(builder.Configuration["ServiceDiscover:ServiceName:Message"]!);
        options.Address = new Uri(address);
    })
    .AddCallCredentials((context, metadata) =>
    {
        metadata.Add("id", builder.Configuration["RPCHeader:ID"]!);
        metadata.Add("jwt", builder.Configuration["RPCHeader:JWT"]!);
        return Task.CompletedTask;
    })
    .ConfigureChannel(options => options.UnsafeUseInsecureChannelCallCredentials = true)
    ;

//配置WebSocketsManager（将所有建立连接的WebSockets当作Static资源进行处理）
builder.Services.AddSingleton<WebSocketsManager>();

//配置Filters
builder.Services.AddScoped<JWTAuthFilterService>();

var app = builder.Build();

//使用Serilog处理请求日志
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//配置WebSocket
var webSocketOptions = new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromMinutes(2)
};
app.UseWebSockets(webSocketOptions);

//app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
