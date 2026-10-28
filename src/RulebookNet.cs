using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using BabyStepsNetworking.Client;
using BabyStepsNetworking.Extensions;
using BabyStepsNetworking.Transport;
using MelonLoader;
using UnityEngine;

namespace Rulebook;

// body sizes in multiplayer (the Baby Steps Multiplayer mod, optional). it sends only bone positions and
// rotations, so other players see a resized nate at normal bone size: squashed or stretched. Rulebook players send
// their bone sizes over a mod channel (the server relays mod packets to everyone by itself) and size each other's
// nates to match. players without Rulebook ignore the packets and still see the stretched version.
// only touched when the multiplayer mod is installed
static class RulebookNet
{
    const string ModId = "waze3174.rulebook";
    static readonly byte[] Magic = { (byte)'R', (byte)'B', 1 };   // mod opcodes are handed out in order, so check it's ours
    const float ResendEvery = 5f;                                    // for players who join later

    static NetworkClient client;
    static IModChannel channel;
    static FieldInfo clientField;
    static float nextSend;
    static float[] lastSent;
    static readonly ConcurrentQueue<(byte uuid, float[] scales)> inbox = new();
    static readonly Dictionary<byte, float[]> sizes = new();          // player uuid -> bone group scales
    static readonly Dictionary<IntPtr, float[]> applied = new();      // remote nate root -> what we last set on it (null = normal)

    // connected to a server: the multiplayer mod colours nate then
    internal static bool Online()
    {
        var nm = BabyStepsMultiplayerClient.Core.networkManager;
        return nm != null && nm.IsConnected;
    }

    internal static void Update()
    {
        var nm = BabyStepsMultiplayerClient.Core.networkManager;
        if (nm == null) return;
        clientField ??= typeof(BabyStepsMultiplayerClient.Networking.NetworkManager).GetField("_networkClient", BindingFlags.NonPublic | BindingFlags.Instance);
        var c = clientField?.GetValue(nm) as NetworkClient;
        if (c != client)
        {
            // a new connection: its own opcode table, so a new channel
            client = c; channel = null; lastSent = null; sizes.Clear();
            if (client != null)
            {
                channel = client.CreateModChannel(ModId);
                if (channel != null) channel.DataReceived += (uuid, data) => { var s = Read(data); if (s != null) inbox.Enqueue((uuid, s)); };
            }
        }
        if (client == null || channel == null || !client.IsConnected) return;

        // send ours when it changes, and now and then for players who joined after
        var mine = Core.BoneScales();
        if (!Same(mine, lastSent) || Time.unscaledTime >= nextSend)
        {
            channel.Send(Write(mine), PacketDelivery.Reliable);
            lastSent = mine; nextSend = Time.unscaledTime + ResendEvery;
        }

        while (inbox.TryDequeue(out var msg)) sizes[msg.uuid] = msg.scales;
        foreach (var kv in nm.players) Apply(kv.Key, kv.Value);
    }

    static bool Same(float[] a, float[] b)
    {
        if (a == null || b == null) return a == b;
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
        return true;
    }

    static byte[] Write(float[] scales)
    {
        var b = new byte[Magic.Length + 1 + scales.Length * 4];
        Buffer.BlockCopy(Magic, 0, b, 0, Magic.Length);
        b[Magic.Length] = (byte)scales.Length;
        Buffer.BlockCopy(scales, 0, b, Magic.Length + 1, scales.Length * 4);
        return b;
    }

    static float[] Read(byte[] b)
    {
        if (b == null || b.Length < Magic.Length + 1) return null;
        for (int i = 0; i < Magic.Length; i++) if (b[i] != Magic[i]) return null;
        int n = b[Magic.Length];
        if (n != Core.BoneGroups || b.Length < Magic.Length + 1 + n * 4) return null;
        var s = new float[n];
        Buffer.BlockCopy(b, Magic.Length + 1, s, 0, n * 4);
        for (int i = 0; i < n; i++) if (float.IsNaN(s[i]) || s[i] < 0.1f || s[i] > 10f) return null;
        return s;
    }

    // size a remote nate's bones like the sender's (normal if they don't have Rulebook, once we've touched it)
    static void Apply(byte uuid, BabyStepsMultiplayerClient.Player.RemotePlayer p)
    {
        var bones = p?.boneChildren;
        if (bones == null || bones.Count == 0 || bones[0] == null) return;
        var id = bones[0].Pointer;
        float[] s = sizes.TryGetValue(uuid, out var got) ? got : null;
        bool had = applied.TryGetValue(id, out var was);
        if (had && Same(was, s)) return;
        if (s == null && !had) return;   // never touched, nothing to undo
        applied[id] = s;
        Core.ScaleBones(bones, s);
    }
}
