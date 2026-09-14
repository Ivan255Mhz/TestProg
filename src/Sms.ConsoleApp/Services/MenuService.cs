using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sms.Contracts.Models;
using Sms.ConsoleApp.Data;

namespace Sms.ConsoleApp.Services;

public sealed class MenuService(AppDbContext dbContext, ILogger<MenuService> logger)
{
    public async Task<IReadOnlyList<MenuItemEntity>> UpdateMenuFromServerAsync(
        IReadOnlyList<MenuItem> freshMenu,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        await dbContext.MenuItems.ExecuteDeleteAsync(cancellationToken);

        var entities = freshMenu.Select(item => new MenuItemEntity
        {
            Id = item.Id,
            Article = item.Article,
            Name = item.Name,
            Price = item.Price,
            IsWeighted = item.IsWeighted,
            FullPath = item.FullPath,
        });

        dbContext.MenuItems.AddRange(entities);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation("Menu saved: {Count} items", freshMenu.Count);
        return await dbContext.MenuItems
            .AsNoTracking()
            .OrderBy(e => e.Name)
            .ToListAsync(cancellationToken);
    }
}
