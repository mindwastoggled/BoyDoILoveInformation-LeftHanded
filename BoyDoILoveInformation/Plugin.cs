using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Reflection;
using BepInEx;
using BoyDoILoveInformation.Core;
using BoyDoILoveInformation.Tools;
using ExitGames.Client.Photon;
using HarmonyLib;
using Newtonsoft.Json;
using Photon.Pun;
using UnityEngine;

namespace BoyDoILoveInformation;

[BepInPlugin(Constants.PluginGuid, Constants.PluginName, Constants.PluginVersion)]
public class Plugin : BaseUnityPlugin
{
    private const string GorillaInfoEndPointURL =
            "https://raw.githubusercontent.com/HanSolo1000Falcon/GorillaInfo/main/";

    public static Dictionary<string, string> KnownCheats;
    public static Dictionary<string, string> KnownMods;
    public static string[]                   HanSoloPlayerIDs;

    public static Color MainColour;
    public static Color SecondaryColour;

    public static AssetBundle BDILIBundle;

    public static Shader UberShader;

    public static AudioSource PluginAudioSource;

    public static AudioClip BDILIClick;

    private string outdatedVersionText;
    private bool   isDeprecatedVersion;
    private float  lastNotification;

    private void Start()
    {
        new Harmony(Constants.PluginGuid).PatchAll();
        GorillaTagger.OnPlayerSpawned(OnGameInitialized);

        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { "BoyDoILoveInformation Public", true }, });
    }

    public static void PlaySound(AudioClip audioClip)
    {
        if (audioClip != null && PluginAudioSource != null)
            PluginAudioSource.PlayOneShot(audioClip);
    }

    private void OnGameInitialized()
    {
        Stream bundleStream = Assembly.GetExecutingAssembly()
                                      .GetManifestResourceStream("BoyDoILoveInformation.Resources.bdilipublic");

        BDILIBundle = AssetBundle.LoadFromStream(bundleStream);
        bundleStream?.Close();

        UberShader = Shader.Find("GorillaTag/UberShader");
        
        gameObject.AddComponent<Notifications>();
        
        using HttpClient client = new();
        HttpResponseMessage response = client
                                      .GetAsync(
                                               "https://raw.githubusercontent.com/HanSolo1000Falcon/GorillaInfo/main/BDILIVersion")
                                      .Result;

        response.EnsureSuccessStatusCode();
        using Stream       stream   = response.Content.ReadAsStreamAsync().Result;
        using StreamReader reader   = new(stream);
        string             contents = reader.ReadToEnd().Trim();

        string[] parts = contents.Split(";");

        Version mostUpToDateVersion = new(parts[0]);
        Version currentVersion      = new(Constants.PluginVersion);

        if (currentVersion < mostUpToDateVersion)
        {
            outdatedVersionText = parts[1];
            isDeprecatedVersion = true;

            return;
        }
        
        FetchModsAndCheatsAndPlayerIDs();

        PluginAudioSource              = new GameObject("LocalAudioSource").AddComponent<AudioSource>();
        PluginAudioSource.spatialBlend = 0f;
        PluginAudioSource.playOnAwake  = false;

        BDILIClick = LoadWavFromResource("BoyDoILoveInformation.Resources.ButtonPressWood.wav");
        
        gameObject.AddComponent<BDILIUtils>();
        gameObject.AddComponent<MenuHandler>();
    }

    private void FetchModsAndCheatsAndPlayerIDs()
    {
        using HttpClient httpClient = new();
        HttpResponseMessage gorillaInfoEndPointResponse =
                httpClient.GetAsync(GorillaInfoEndPointURL + "KnownCheats.txt").Result;

        using (Stream stream = gorillaInfoEndPointResponse.Content.ReadAsStreamAsync().Result)
        {
            using (StreamReader reader = new(stream))
            {
                KnownCheats = JsonConvert.DeserializeObject<Dictionary<string, string>>(reader.ReadToEnd());
            }
        }

        HttpResponseMessage knownModsEndPointResponse =
                httpClient.GetAsync(GorillaInfoEndPointURL + "KnownMods.txt").Result;

        using (Stream stream = knownModsEndPointResponse.Content.ReadAsStreamAsync().Result)
        {
            using (StreamReader reader = new(stream))
            {
                KnownMods = JsonConvert.DeserializeObject<Dictionary<string, string>>(reader.ReadToEnd());
            }
        }
        
        HttpResponseMessage hanSoloPlayerIdsEndPointResponse =
                httpClient.GetAsync(GorillaInfoEndPointURL + "HanSoloPlayerId").Result;

        using (Stream stream = knownModsEndPointResponse.Content.ReadAsStreamAsync().Result)
        {
            using (StreamReader reader = new(stream))
            {
                HanSoloPlayerIDs = JsonConvert.DeserializeObject<string[]>(reader.ReadToEnd());
            }
        }
    }

    private AudioClip LoadWavFromResource(string resourcePath)
    {
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourcePath);

        if (stream == null)
            return null;

        byte[] buffer = new byte[stream.Length];
        int    read   = stream.Read(buffer, 0, buffer.Length);

        WAV     wav = new(buffer);
        float[] samples;

        if (wav.ChannelCount == 2)
        {
            samples = new float[wav.SampleCount];
            for (int i = 0; i < wav.SampleCount; i++)
                samples[i] = (wav.LeftChannel[i] + wav.RightChannel[i]) * 0.5f;
        }
        else
        {
            samples = wav.LeftChannel;
        }

        AudioClip audioClip = AudioClip.Create(resourcePath, wav.SampleCount, 1, wav.Frequency, false);
        audioClip.SetData(samples, 0);

        return audioClip;
    }

    private void Update()
    {
        if (!isDeprecatedVersion || Time.time - lastNotification < 10f)
            return;
        
        lastNotification = Time.time;
        Notifications.SendNotification(outdatedVersionText);
    }
}