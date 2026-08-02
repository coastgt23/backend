namespace Stella
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                Args = args
            });

            builder.Configuration.Sources
                .OfType<Microsoft.Extensions.Configuration.Json.JsonConfigurationSource>()
                .ToList()
                .ForEach(x => x.ReloadOnChange = false);

            builder.Services.AddAuthentication();
            builder.Services.AddAuthorization();

            var webApplication = builder.Build();
            var app = new ServerAPI(webApplication);

            app.Init();

            Signatures.Init();

            app.Run();
        }
    }
}