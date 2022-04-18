// See https://aka.ms/new-console-template for more information

using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.CosmosRepository;
using Microsoft.Azure.CosmosRepository.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PatchUpdate;

var configBuilder = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

configBuilder.AddConfiguration(configBuilder.Build().GetSection($"RepositoryOptions"));
var configuration = configBuilder.Build();

await RunPatchUpdateDemo(configuration);

IRepository<BankAccount> BuildRepository(IConfiguration configuration) 
{
    ServiceProvider provider = new ServiceCollection().AddCosmosRepository(
            options => configuration.GetSection($"RepositoryOptions")
                .Bind(options),
            cosmosClientOptions =>
            {
                var serializationOptions = configuration.GetSection($"RepositoryOptions").Get<RepositoryOptions>().SerializationOptions;
                cosmosClientOptions.SerializerOptions.PropertyNamingPolicy = serializationOptions.PropertyNamingPolicy;
                cosmosClientOptions.SerializerOptions.IgnoreNullValues = serializationOptions.IgnoreNullValues;
            })
        .AddSingleton<IConfiguration>(configuration)
        .BuildServiceProvider();

    IRepository<BankAccount> repository = provider.GetRequiredService<IRepository<BankAccount>>();

    return repository;
}
async Task RunPatchUpdateDemo(IConfiguration configuration)
{
    IRepository<BankAccount> repository = BuildRepository(configuration);

    BankAccount currentBankAccount = await repository.CreateAsync(new BankAccount()
    {
        Name = "Current Account",
        Balance = 500.0,
        TimeToLive = TimeSpan.FromHours(4)
    });

    try
    {
        await repository.UpdateAsync(currentBankAccount.Id,
            builder => builder.Replace(account => account.Balance, currentBankAccount.Balance - 250), etag: currentBankAccount.Etag);
    }
    catch (CosmosException exception) when (exception.StatusCode == HttpStatusCode.PreconditionFailed)
    {
        Console.WriteLine($"{exception.Message}");
    }
}