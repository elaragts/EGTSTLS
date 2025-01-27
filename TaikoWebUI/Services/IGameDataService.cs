﻿using System.Collections.Immutable;

namespace TaikoWebUI.Services;

public interface IGameDataService
{
    public Task InitializeAsync(string dataBaseUrl);

    public Task<Dictionary<uint, MusicDetail>> GetMusicDetailDictionary();
    
    public Task<Dictionary<uint, Title>> GetTitleDictionary();
    
    public Task<List<Costume>> GetCostumeList();
    
    public Task<Dictionary<string, List<uint>>> GetLockedCostumeDataDictionary();
    
    public Task<Dictionary<string, List<uint>>> GetLockedTitleDataDictionary();

    public string GetMusicNameBySongId(Dictionary<uint, MusicDetail> musicDetails,uint songId, string? language = null);

    public string GetMusicArtistBySongId(Dictionary<uint, MusicDetail> musicDetails,uint songId, string? language = null);

    public SongGenre GetMusicGenreBySongId(Dictionary<uint, MusicDetail> musicDetails,uint songId);

    public int GetMusicIndexBySongId(Dictionary<uint, MusicDetail> musicDetails,uint songId);

    public ImmutableDictionary<uint, DanData> GetDanMap();

    public int GetMusicStarLevel(Dictionary<uint, MusicDetail> musicDetails, uint songId, Difficulty difficulty);

    public string GetHeadTitle(IEnumerable<Costume> costumes, uint index, string? language = "ja");
    public string GetKigurumiTitle(IEnumerable<Costume> costumes, uint index, string? language = "ja");
    public string GetBodyTitle(IEnumerable<Costume> costumes, uint index, string? language = "ja");
    public string GetFaceTitle(IEnumerable<Costume> costumes, uint index, string? language = "ja");
    public string GetPuchiTitle(IEnumerable<Costume> costumes, uint index, string? language = "ja");
}