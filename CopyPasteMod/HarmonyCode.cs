/*▄▄▄    ███▄ ▄███▓  ▄████  ▄▄▄██▀▀▀▓█████▄▄▄█████▓
▓█████▄ ▓██▒▀█▀ ██▒ ██▒ ▀█▒   ▒██   ▓█   ▀▓  ██▒ ▓▒
▒██▒ ▄██▓██    ▓██░▒██░▄▄▄░   ░██   ▒███  ▒ ▓██░ ▒░
▒██░█▀  ▒██    ▒██ ░▓█  ██▓▓██▄██▓  ▒▓█  ▄░ ▓██▓ ░ 
░▓█  ▀█▓▒██▒   ░██▒░▒▓███▀▒ ▓███▒   ░▒████▒ ▒██▒ ░ 
░▒▓███▀▒░ ▒░   ░  ░ ░▒   ▒  ▒▓▒▒░   ░░ ▒░ ░ ▒ ░░*/
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection.Emit;
using UnityEngine;
using Physics = UnityEngine.Physics;
using Pool = Facepunch.Pool;

namespace CopyPasteMod
{
    [HarmonyPatch(typeof(ConsoleSystem), "Run", typeof(ConsoleSystem.Option), typeof(string), typeof(object[]))]
    internal class ConsoleSystem_BuildCommand //Console Command
    {
        private static async void UploadFile(string url, string path, string name)
        {
            //Send HTTP Post data to webserver
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    using (MultipartFormDataContent content = new MultipartFormDataContent())
                    {
                        // Read the file bytes and add it to the content
                        byte[] fileBytes = File.ReadAllBytes(path);
                        ByteArrayContent fileContent = new ByteArrayContent(fileBytes);
                        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                        // Add file content and name it according to the server requirements (e.g., "file")
                        content.Add(fileContent, "file", Path.GetFileName(path));
                        HttpResponseMessage response = await client.PostAsync(url, content);
                        // Check the response
                        if (response.IsSuccessStatusCode)
                        {
                            Debug.Log("File uploaded successfully!");
                        }
                        else
                        {
                            Debug.Log($"Failed to upload file. Status: {response.StatusCode}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Exception: {ex.Message}");
                return;
            }
        }

        //Give Entity to Player
        private static void GiveOwner(BaseEntity entity, BasePlayer gplayer)
        {
            ProtoBuf.PlayerNameID pl = new ProtoBuf.PlayerNameID();
            pl.userid = gplayer.userID;
            pl.username = gplayer.displayName;
            try
            {
                if (entity is BuildingPrivlidge)
                {
                    BuildingPrivlidge bp = entity as BuildingPrivlidge;
                    bp.OwnerID = gplayer.userID;
                    bp.authorizedPlayers.Add(pl);
                    bp.AddPlayer(gplayer, gplayer.userID);
                    return;
                }
                if (entity is VehiclePrivilege)
                {
                    VehiclePrivilege vp = entity as VehiclePrivilege;
                    vp.OwnerID = gplayer.userID;
                    vp.AddPlayer(gplayer);
                    return;
                }
            }
            catch { }
            try
            {
                if (entity is SleepingBag)
                {
                    SleepingBag sleepingBag = entity as SleepingBag;
                    sleepingBag.OwnerID = gplayer.userID;
                    sleepingBag.deployerUserID = gplayer.userID;
                    return;
                }
            }
            catch { }
            try
            {
                if (entity is Door)
                {
                    Door door = entity as Door;
                    entity.OwnerID = pl.userid;
                    var lockSlot = door.GetSlot(BaseEntity.Slot.Lock);
                    if (lockSlot is CodeLock)
                    {
                        var codeLock = (CodeLock)lockSlot;
                        entity.OwnerID = pl.userid;
                        if (!codeLock.whitelistPlayers.Contains(pl.userid))
                        {
                            codeLock.whitelistPlayers.Add(pl.userid);
                        }
                        if (!codeLock.IsLocked())
                        {
                            codeLock.SetFlag(BaseEntity.Flags.Locked, true);
                        }
                    }
                    if (door.IsOpen())
                    {
                        door.CloseRequest();
                    }
                    return;
                }
            }
            catch { }
            try
            {
                if (entity is GunTrap)
                {
                    GunTrap gt = entity as GunTrap;
                    gt.OwnerID = gplayer.userID;
                    return;
                }
            }
            catch { }
            try
            {
                if (entity is AutoTurret)
                {
                    AutoTurret at = entity as AutoTurret;
                    at.OwnerID = gplayer.userID;
                    bool alreadyadded = false;
                    foreach (ProtoBuf.PlayerNameID pid in at.authorizedPlayers)
                    {
                        if (pid.userid == gplayer.userID)
                        {
                            alreadyadded = true;
                            break;
                        }
                    }
                    if (!alreadyadded) { at.authorizedPlayers.Add(pl); }
                    return;
                }
            }
            catch { }
            try
            {
                if (entity is SamSite)
                {
                    SamSite ss = entity as SamSite;
                    ss.OwnerID = gplayer.userID;
                    return;
                }
            }
            catch { }
            try
            {
                if (entity is StorageContainer)
                {
                    StorageContainer sc = entity as StorageContainer;
                    entity.OwnerID = pl.userid;
                    var lockSlot = sc.GetSlot(BaseEntity.Slot.Lock);
                    if (lockSlot is CodeLock)
                    {
                        var codeLock = (CodeLock)lockSlot;
                        entity.OwnerID = pl.userid;
                        codeLock.whitelistPlayers.Add(pl.userid);
                        if (!codeLock.IsLocked())
                        {
                            codeLock.SetFlag(BaseEntity.Flags.Locked, true);
                        }
                    }
                    return;
                }
            }
            catch { }
            try
            {
                if (entity is BaseCombatEntity)
                {
                    BaseCombatEntity bce = entity as BaseCombatEntity;
                    bce.OwnerID = gplayer.userID;
                    bce.SetCreatorEntity(gplayer);
                }
            }
            catch { }
        }

        //Catch console commands
        [HarmonyPrefix]
        static bool Prefix(ConsoleSystem.Option options, string strCommand, params object[] args)
        {
            try
            {
                if (options.Connection.authLevel > 1)
                {
                    if (strCommand.StartsWith("uploadpaste")) //Upload Command
                    {
                        //Validate Args
                        string[] cargs = strCommand.Split(' ');
                        if (cargs.Length == 3)
                        {
                            string url = cargs[1];
                            if (!url.StartsWith("http"))
                            {
                                Debug.Log("Invalid URL " + url);
                                return false;
                            }
                            string name = cargs[2];
                            //Validate file
                            if (string.IsNullOrEmpty(name))
                            {
                                name = Path.GetFileNameWithoutExtension(url);
                            }
                            string path = ConVar.Server.GetServerFolder("copypaste") + "/" + name + ".data";
                            if (File.Exists(path))
                            {
                                Debug.Log("Uploading Paste " + name + " to " + url);
                                //Upload
                                UploadFile(url, path, name);
                                return false;
                            }
                            Debug.Log("Paste doesn't exsist " + name + " save it to file first if you havn't already");
                            return false;
                        }
                        Debug.Log("uploadpaste <url> <name>");
                        return false;
                    }
                    if (strCommand.StartsWith("givebase")) //Give all baseparts to player
                    {
                        //Validate args
                        string[] cargs = strCommand.Split(' ');
                        if (cargs.Length > 1)
                        {
                            ulong userid = 0;
                            if (!ulong.TryParse(cargs[1], out userid)) { Debug.Log("Invalid SteamID"); return false; }
                            if (userid == 0) { return false; }
                            BasePlayer basePlayer = BasePlayer.FindAwakeOrSleepingByID(options.Connection.userid);
                            BasePlayer targetPlayer = BasePlayer.FindAwakeOrSleepingByID(userid);
                            if (basePlayer != null)
                            {
                                Debug.Log("Giving Ownership Of Base");
                                List<BaseEntity> Changed = Pool.Get<List<BaseEntity>>();
                                //Find buildingblock being looked at
                                RaycastHit hit;
                                var raycast = Physics.Raycast(basePlayer.eyes.HeadRay(), out hit, 5, -1);
                                BaseEntity entity = raycast ? hit.GetEntity() : null;
                                if (entity is BuildingBlock)
                                {
                                    BuildingBlock bb = entity as BuildingBlock;
                                    if (bb != null)
                                    {
                                        //Set TC
                                        try { Changed.Add(bb.GetBuildingPrivilege()); bb.GetBuildingPrivilege().OwnerID = userid; } catch { }
                                        //Set all building blocks
                                        foreach (var bbe in bb.GetBuilding().buildingBlocks)
                                        {
                                            if (!Changed.Contains(bbe))
                                            {
                                                bbe.OwnerID = userid;
                                                Changed.Add(bbe);
                                                //Set child ents
                                                if (bbe.children.Count > 0)
                                                {
                                                    foreach (var child in bbe.children)
                                                    {
                                                        if (!Changed.Contains(child))
                                                        {
                                                            child.OwnerID = userid;
                                                            Changed.Add(child);
                                                        }
                                                    }
                                                }
                                            }
                                            //Vis scan to catch ents in rooms
                                            List<BaseEntity> baseEntities = Pool.Get<List<BaseEntity>>();
                                            Vis.Entities<BaseEntity>(bbe.transform.position, 3, baseEntities, -1);
                                            if (baseEntities?.Count > 0)
                                            {
                                                foreach (var b in baseEntities)
                                                {
                                                    if(b is BasePlayer || b is ResourceEntity || b is BushEntity) { continue; }
                                                    if (!Changed.Contains(b))
                                                    {
                                                        b.OwnerID = userid;
                                                        Changed.Add(b);
                                                        GiveOwner(b, targetPlayer);
                                                    }
                                                }
                                            }
                                            Pool.FreeUnmanaged(ref baseEntities);
                                        }
                                        Debug.Log(string.Format("Gave {0} {1} building blocks/entite", (targetPlayer == null ? targetPlayer.displayName : userid.ToString()), Changed.Count.ToString()));
                                        return false;
                                    }
                                }
                                Debug.Log("No building block found");
                                return false;
                            }
                        }
                        Debug.Log("givebase <steamid>");
                        return false;
                    }
                }
            }
            catch { }
            return true;
        }
    }

    [HarmonyPatch(typeof(ConVar.CopyPaste), "CopyEntities", typeof(List<BaseEntity>), typeof(Vector3), typeof(Quaternion))]
    internal class CopyPaste_CopyEntities
    {
        //Each building block does a vis scan to find any ents close to it to capture everything
        [HarmonyPrefix]
        static void Prefix(List<BaseEntity> entities)
        {
            for (int i = entities.Count - 1; i >= 0; i--)
            {
                if (entities[i] != null && entities[i] is BuildingBlock)
                {
                    List<BaseEntity> list = Pool.Get<List<BaseEntity>>();
                    Vis.Entities<BaseEntity>(entities[i].transform.position, 3, list, -1, QueryTriggerInteraction.Collide);
                    foreach (var ent in list) { if (!entities.Contains(ent) && !(ent is BasePlayer) && !(ent is ResourceEntity) && !(ent is BushEntity)) { entities.Add(ent); } } //Ignore players, world resources, bushes
                    Pool.FreeUnmanaged(ref list);
                }
            }
        }

        //Output message on copy
        [HarmonyPostfix]
        static void Postfix(List<BaseEntity> entities)
        {
            Debug.Log("Copied " + entities.Count + " entities");
        }
    }

    [HarmonyPatch(typeof(ConVar.CopyPaste), "PasteEntities")]
    internal class CopyPaste_PasteEntities
    {
        private class EntityWrapper { public BaseEntity Entity; public ProtoBuf.Entity Protobuf; public Vector3 Position; public Quaternion Rotation; public bool HasParent; }
        //Stop bases auto clipping to the ground. (Fixes wire floating fault and cave bases breaking)
        //Fix container skin colours not being set
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> o = instructions.ToList<CodeInstruction>();
            for (int i = 0; i < o.Count; i++)
            {
                if (o[i].ToString().Contains("foundation"))
                {
                    o[i].operand = "";
                    continue;
                }
                if (o[i].ToString().Contains("RefreshEntityLinks"))
                {
                    o.Insert(i - 1, new CodeInstruction(OpCodes.Dup));
                    o.Insert(i, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(CopyPaste_PasteEntities), nameof(ContainerColour))));
                    break;
                }
            }
            return o;
        }

        private static void ContainerColour(EntityWrapper a)
        {
            //Set colour from simpleUint
            try
            {
                if (a != null)
                {
                    if (a?.Entity != null && a?.Entity is BuildingBlock block)
                    {
                        if (block != null && a?.Protobuf?.simpleUint?.value != null)
                        {
                            block.SetCustomColour(a.Protobuf.simpleUint.value);
                        }
                    }
                }
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(ConVar.CopyPaste), "CanPrefabBePasted")]
    internal class CopyPaste_CanPrefabBePasted
    {
        //Remove debug spam
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> o = instructions.ToList<CodeInstruction>();
            for (int i = 0; i < o.Count; i++)
            {
                if (o[i].ToString().Contains("Checking prefab "))
                {
                    for (int j = 0; j < 6; j++)
                    {
                        o[i + j].opcode = OpCodes.Nop;
                        o[i + j].operand = null;
                    }
                    break;
                }
            }
            return o;
        }
    }

    [HarmonyPatch(typeof(ConVar.CopyPaste), "SaveEntity")]
    internal class CopyPaste_SaveEntity
    {
        //Remove debug spam
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> o = instructions.ToList<CodeInstruction>();
            for (int i = 0; i < o.Count; i++)
            {
                if (o[i].ToString().Contains("Saving "))
                {
                    for (int j = 0; j < 15; j++)
                    {
                        o[i + j].opcode = OpCodes.Nop;
                        o[i + j].operand = null;
                    }
                    break;
                }
            }
            return o;
        }
    }
}