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
using Pool = Facepunch.Pool;

namespace CopyPasteMod
{
    [HarmonyPatch(typeof(ConsoleSystem), "Run", typeof(ConsoleSystem.Option), typeof(string), typeof(object[]))]
    internal class ConsoleSystem_BuildCommand //Console Command
    {
        private static async void UploadFile(string url, string path,string name)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    // Create MultipartFormDataContent for the file
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

        [HarmonyPrefix]
        static bool Prefix(ConsoleSystem.Option options, string strCommand, params object[] args)
        {
            try
            {
                if (options.Connection.authLevel > 1 && strCommand.StartsWith("uploadpaste"))
                {
                    Debug.LogWarning(strCommand);
                    string[] cargs = strCommand.Split(' ');
                    if (cargs.Length == 3)
                    {
                        string url = cargs[1];
                        if(!url.StartsWith("http"))
                        {
                            Debug.Log("Invalid URL " + url);
                            return false;
                        }
                        string name = cargs[2];
                        if (string.IsNullOrEmpty(name))
                        {
                            name = Path.GetFileNameWithoutExtension(url);
                        }
                        string path = ConVar.Server.GetServerFolder("copypaste") + "/" + name + ".data";
                        if (File.Exists(path))
                        {
                            Debug.Log("Uploading Paste " + name + " to " + url);
                            UploadFile(url, path,name);
                            return false;
                        }
                        Debug.Log("Paste doesn't exsist " + name + " save it to file first if you havn't already");
                        return false;
                    }
                    Debug.Log("uploadpaste <url> <name>");
                    return false;
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
                    Vis.Entities<BaseEntity>(entities[i].transform.position, 5, list, -1, QueryTriggerInteraction.Collide);
                    foreach (var ent in list) { if (!entities.Contains(ent) && !(ent is BasePlayer)) { entities.Add(ent); } } //Ignore players
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