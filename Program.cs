namespace Stella
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

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