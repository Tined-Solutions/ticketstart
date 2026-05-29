using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TicketeraOnline.Api.Data;
using Xunit;

namespace TicketeraOnline.Api.Tests;

public class VerifyDatabaseSchema
{
    [Fact]
    public async Task Database_Should_Have_All_Tables()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .Build();

        var connectionString = configuration.GetConnectionString("MigrationConnection");
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        using var context = new ApplicationDbContext(optionsBuilder.Options);
        
        // Verify tables exist by querying them
        var usersExist = await context.Users.AnyAsync() || !await context.Users.AnyAsync();
        var eventsExist = await context.Events.AnyAsync() || !await context.Events.AnyAsync();
        var ticketTypesExist = await context.TicketTypes.AnyAsync() || !await context.TicketTypes.AnyAsync();
        var reservationsExist = await context.Reservations.AnyAsync() || !await context.Reservations.AnyAsync();
        var ticketsExist = await context.Tickets.AnyAsync() || !await context.Tickets.AnyAsync();
        var transactionsExist = await context.Transactions.AnyAsync() || !await context.Transactions.AnyAsync();
        
        Assert.True(usersExist);
        Assert.True(eventsExist);
        Assert.True(ticketTypesExist);
        Assert.True(reservationsExist);
        Assert.True(ticketsExist);
        Assert.True(transactionsExist);
    }
}
