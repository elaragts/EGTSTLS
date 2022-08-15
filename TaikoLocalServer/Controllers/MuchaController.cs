﻿using Microsoft.AspNetCore.Mvc;
using Swan.Formatters;
using TaikoLocalServer.Models;
using TaikoLocalServer.Utils;

namespace TaikoLocalServer.Controllers;

public class MuchaController : ControllerBase
{
    private const string UrlBase = "https://v402-front.mucha-prd.nbgi-amnet.jp:10122";
    private readonly ILogger<MuchaController> logger;
    public MuchaController(ILogger<MuchaController> logger) {
        this.logger = logger;
    }

    [HttpPost("/mucha_front/boardauth.do")]

    public ContentResult BoardAuth([FromForm] MuchaUpdateCheckRequest request)
    {
        logger.LogInformation("Request is {Request}", request.Stringify());
        var serverTime = DateTime.Now.ToString("yyyyMMddHHmm");
        var utcServerTime = DateTime.UtcNow.ToString("yyyyMMddHHmm");
        var response = new Dictionary<string, string>
        {
            { "RESULTS", "001" },
            { "AREA_0", "008" },
            { "AREA_0_EN", "" },
            { "AREA_1", "009" },
            { "AREA_1_EN", "" },
            { "AREA_2", "010" },
            { "AREA_2_EN", "" },
            { "AREA_3", "011" },
            { "AREA_3_EN", "" },
            { "AREA_FULL_0", "" },
            { "AREA_FULL_0_EN", "" },
            { "AREA_FULL_1", "" },
            { "AREA_FULL_1_EN", "" },
            { "AREA_FULL_2", "" },
            { "AREA_FULL_2_EN", "" },
            { "AREA_FULL_3", "" },
            { "AREA_FULL_3_EN", "" },
            { "AUTH_INTERVAL", "86400" },
            { "CHARGE_URL", $"{UrlBase}/charge/" },
            { "CONSUME_TOKEN", "0" },
            { "COUNTRY_CD", "JPN" },
            { "DONGLE_FLG", "1" },
            { "EXPIRATION_DATE", "null" },
            { "FILE_URL", $"{UrlBase}/file/" },
            { "FORCE_BOOT", "0" },
            { "PLACE_ID", request.PlaceId ?? "" },
            { "PREFECTURE_ID", "14" },
            { "SERVER_TIME", serverTime },
            { "UTC_SERVER_TIME", utcServerTime },
            { "SHOP_NAME", "NAMCO" },
            { "SHOP_NAME_EN", "NAMCO" },
            { "SHOP_NICKNAME", "W" },
            { "SHOP_NICKNAME_EN", "W" },
            { "URL_1", $"{UrlBase}/url1/" },
            { "URL_2", $"{UrlBase}/url2/" },
            { "URL_3", $"{UrlBase}/url3/" },
            { "USE_TOKEN", "0" }
        };
        var formOutput = FormOutputUtil.ToFormOutput(response);
        return Content(formOutput);
    }

    [HttpPost("/mucha_front/updatacheck.do")]
    public ContentResult UpdateCheck(MuchaBoardAuthRequest request)
    {
        logger.LogInformation("Request is {Request}", request.Stringify());
        var response = new Dictionary<string, string>
        {
            { "RESULTS", "001" },
            { "UPDATE_VER_1", request.GameVersion ?? "S1210JPN08.18" },
            { "UPDATE_URL_1", $"{UrlBase}/updUrl1/" },
            { "UPDATE_SIZE_1", "0" },
            { "UPDATE_CRC_1", "0000000000000000" },
            { "CHECK_URL_1", $"{UrlBase}/checkUrl/" },
            { "EXE_VER_1", request.GameVersion ?? "S1210JPN08.18" },
            { "INFO_SIZE_1", "0" },
            { "COM_SIZE_1", "0" },
            { "COM_TIME_1", "0" },
            { "LAN_INFO_SIZE_1", "0" }
        };
        var formOutput = FormOutputUtil.ToFormOutput(response);
        return Content(formOutput);
    }
}