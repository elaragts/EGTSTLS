﻿using GameDatabase.Context;
using Throw;

namespace TaikoLocalServer.Handlers;

public record PurchaseSongCommand(uint Baid, uint SongNo, uint Type, uint TokenId, uint Price) : IRequest<CommonSongPurchaseResponse>;

public record PurchaseSongCommandCN(uint Baid, uint SongNo, uint TokenId, uint Price) : IRequest<CommonSongPurchaseResponse>;

public class PurchaseSongCommandHandler(TaikoDbContext context, ILogger<PurchaseSongCommandHandler> logger) 
    : IRequestHandler<PurchaseSongCommand, CommonSongPurchaseResponse>
{

    public async Task<CommonSongPurchaseResponse> Handle(PurchaseSongCommand request, CancellationToken cancellationToken)
    {
        var user = await context.UserData
            .Include(u => u.Tokens)
            .FirstOrDefaultAsync(u => u.Baid == request.Baid, cancellationToken);
        user.ThrowIfNull($"User with baid {request.Baid} does not exist!");
        
        var token = user.Tokens.FirstOrDefault(t => t.Id == request.TokenId);
        
        if (token is not null && token.Count >= request.Price)
        {
            token.Count -= (int)request.Price;
        }
        else
        {
            logger.LogError("User with baid {Baid} does not have enough tokens to purchase song with id {SongNo}!", request.Baid, request.SongNo);
            return new CommonSongPurchaseResponse { Result = 0 };
        }

        if (request.Type == 1)
        {
            if (user.UnlockedUraSongIdList.Contains(request.SongNo))
            {
                logger.LogWarning("User with baid {Baid} already has song with id {SongNo} unlocked!", request.Baid, request.SongNo);
                return new CommonSongPurchaseResponse { Result = 0 };
            }
            
            user.UnlockedUraSongIdList.Add(request.SongNo);
        }
        else
        {
            if (user.UnlockedSongIdList.Contains(request.SongNo))
            {
                logger.LogWarning("User with baid {Baid} already has song with id {SongNo} unlocked!", request.Baid, request.SongNo);
                return new CommonSongPurchaseResponse { Result = 0 };
            }
            
            user.UnlockedSongIdList.Add(request.SongNo);
        }
        
        context.UserData.Update(user);
        await context.SaveChangesAsync(cancellationToken);
        return new CommonSongPurchaseResponse { Result = 1, TokenCount = token.Count };
    }
}


public class PurchaseSongCommandHandlerCN(TaikoDbContext context, ILogger<PurchaseSongCommandHandlerCN> logger) 
    : IRequestHandler<PurchaseSongCommandCN, CommonSongPurchaseResponse>
{

    public async Task<CommonSongPurchaseResponse> Handle(PurchaseSongCommandCN request, CancellationToken cancellationToken)
    {
        var user = await context.UserData
            .Include(u => u.Tokens)
            .FirstOrDefaultAsync(u => u.Baid == request.Baid, cancellationToken);
        user.ThrowIfNull($"User with baid {request.Baid} does not exist!");
        if (user.UnlockedSongIdList.Contains(request.SongNo))
        {
            logger.LogWarning("User with baid {Baid} already has song with id {SongNo} unlocked!", request.Baid, request.SongNo);
            return new CommonSongPurchaseResponse { Result = 0 };
        }
        
        var token = user.Tokens.FirstOrDefault(t => t.Id == request.TokenId);
        if (token is not null && token.Count >= request.Price)
        {
            token.Count -= (int)request.Price;
        }
        else
        {
            logger.LogError("User with baid {Baid} does not have enough tokens to purchase song with id {SongNo}!", request.Baid, request.SongNo);
            return new CommonSongPurchaseResponse { Result = 0 };
        }
        
        user.UnlockedSongIdList.Add(request.SongNo);
        
        context.UserData.Update(user);
        await context.SaveChangesAsync(cancellationToken);
        return new CommonSongPurchaseResponse { Result = 1, TokenCount = token.Count };
    }
}