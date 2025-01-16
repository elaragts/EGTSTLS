﻿using GameDatabase.Context;
using SharedProject.Models;
using Swan.Mapping;
using Throw;

namespace TaikoLocalServer.Services;

public class SongBestDatumService : ISongBestDatumService
{
    private readonly TaikoDbContext context;

    public SongBestDatumService(TaikoDbContext context)
    {
        this.context = context;
    }

    public async Task<List<SongBestDatum>> GetAllSongBestData(uint baid)
    {
        return await context.SongBestData.Where(datum => datum.Baid == baid).ToListAsync();
    }

    public async Task<List<SongBestData>> GetAllSongBestAsModel(uint baid)
    {
        var songbestDbData = await context.SongBestData.Where(datum => datum.Baid == baid).ToListAsync();

        var result = songbestDbData.Select(datum => datum.CopyPropertiesToNew<SongBestData>()).ToList();

        var aiSectionBest = await context.AiScoreData.Where(datum => datum.Baid == baid).Include(datum => datum.AiSectionScoreData).ToListAsync();

        var playLogs = await context.SongPlayData.Where(datum => datum.Baid == baid).ToListAsync();
        foreach (var bestData in result)
        {
            var songPlayDatums = playLogs.Where(datum => datum.Difficulty == bestData.Difficulty && datum.SongId == bestData.SongId).ToArray();
            songPlayDatums.Throw($"Play log for song id {bestData.SongId} is null! " + "Something is wrong with db!").IfEmpty();

            bestData.LastPlayTime = songPlayDatums.MaxBy(datum => datum.PlayTime)!.PlayTime;

            var bestLog = songPlayDatums.MaxBy(datum => datum.Score);
            bestLog.CopyOnlyPropertiesTo(bestData,
                nameof(SongPlayDatum.PlayTime),
                nameof(SongPlayDatum.GoodCount),
                nameof(SongPlayDatum.OkCount),
                nameof(SongPlayDatum.MissCount),
                nameof(SongPlayDatum.HitCount),
                nameof(SongPlayDatum.DrumrollCount),
                nameof(SongPlayDatum.ComboCount)
            );

            var aiSection = aiSectionBest.FirstOrDefault(datum => datum.Difficulty == bestData.Difficulty &&
                                                         datum.SongId == bestData.SongId);
            if (aiSection is null)
            {
                continue;
            }

            bestData.AiSectionBestData = aiSection.AiSectionScoreData
                .Select(datum => datum.CopyPropertiesToNew<AiSectionBestData>()).ToList();
        }

        return result;
    }
}