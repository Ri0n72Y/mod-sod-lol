using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace SodLolCaitlyn;

internal sealed class CaitlynResourceRegistry
{
    private sealed class RuntimeEntry
    {
        public CaitlynSkillDefinition Definition;
        public uint AssetId;
        public GameObject Prefab;
        public SkillTrigger Skill;
    }

    private readonly Dictionary<string, RuntimeEntry> _byGuid = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Type, RuntimeEntry> _byType = new();
    private bool _registered;

    public void Register()
    {
        if (_registered)
        {
            return;
        }

        DewInternal.DewResourceDatabase database = DewResources.database;
        if (database == null)
        {
            throw new InvalidOperationException("DewResources.database is unavailable.");
        }

        ValidateNoCollisions(database);

        try
        {
            foreach (CaitlynSkillDefinition definition in CaitlynSkillDefinition.All)
            {
                RuntimeEntry entry = CreateEntry(definition);
                try
                {
                    _byGuid.Add(definition.Guid, entry);
                    _byType.Add(definition.RuntimeType, entry);

                    string aqn = GetAssemblyQualifiedName(definition);
                    database.typeAssemblyQualifiedNameToGuid.Add(aqn, definition.Guid);
                    database.netObjectAssetIdToGuid.Add(entry.AssetId, definition.Guid);
                    database.allGuids.Add(definition.Guid);
                }
                catch
                {
                    CleanupEntry(entry, database);
                    throw;
                }
            }

            database.InitForRuntime();
            VerifyConfigIsolation();
            _registered = true;
        }
        catch (Exception exception)
        {
            try
            {
                Rollback(database);
            }
            catch (Exception rollbackException)
            {
                Debug.LogError($"[SodLolCaitlyn] Runtime-resource rollback also failed: {rollbackException}");
            }

            throw new InvalidOperationException(
                "Failed to register Caitlyn runtime resources. Partial registrations were rolled back where possible.",
                exception);
        }
    }

    public void Unregister()
    {
        if (!_registered && _byGuid.Count == 0)
        {
            return;
        }

        DewInternal.DewResourceDatabase database = DewResources.database;
        if (database == null)
        {
            foreach (RuntimeEntry entry in SnapshotEntries())
            {
                CaitlynNetworkHelper.UnregisterSpawnHandler(entry.AssetId);
                DestroyPrefab(entry);
            }
            _byGuid.Clear();
            _byType.Clear();
            _registered = false;
            return;
        }

        Rollback(database);
        _registered = false;
    }

    public bool TryLoad(string guid, out UnityEngine.Object result)
    {
        result = null;
        if (string.IsNullOrEmpty(guid) || !_byGuid.TryGetValue(guid, out RuntimeEntry entry))
        {
            return false;
        }
        if (entry.Prefab == null)
        {
            throw new InvalidOperationException($"Runtime prefab for {entry.Definition.TypeName} was destroyed unexpectedly.");
        }

        CaitlynNetworkHelper.EnsureNetworkIdentity(entry.Prefab, entry.AssetId);
        result = entry.Prefab;
        return true;
    }

    public T GetPrefab<T>() where T : SkillTrigger
    {
        return _byType.TryGetValue(typeof(T), out RuntimeEntry entry) ? entry.Skill as T : null;
    }

    public SkillTrigger GetPrefab(CaitlynSkillDefinition definition)
    {
        return definition != null && _byType.TryGetValue(definition.RuntimeType, out RuntimeEntry entry)
            ? entry.Skill
            : null;
    }

    private static RuntimeEntry CreateEntry(CaitlynSkillDefinition definition)
    {
        uint assetId = CaitlynNetworkHelper.GenerateAssetId(definition.Guid);
        GameObject prefab = new(definition.TypeName);

        try
        {
            prefab.SetActive(false);
            prefab.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(prefab);

            CaitlynNetworkHelper.EnsureNetworkIdentity(prefab, assetId);
            SkillTrigger skill = (SkillTrigger)prefab.AddComponent(definition.RuntimeType);
            ConfigureFromVanillaTemplate(skill, definition);

            try
            {
                CaitlynNetworkHelper.RegisterSpawnHandler(assetId, prefab);
            }
            catch
            {
                CaitlynNetworkHelper.UnregisterSpawnHandler(assetId);
                throw;
            }

            return new RuntimeEntry
            {
                Definition = definition,
                AssetId = assetId,
                Prefab = prefab,
                Skill = skill
            };
        }
        catch
        {
            if (prefab != null)
            {
                UnityEngine.Object.DestroyImmediate(prefab);
            }
            throw;
        }
    }

    private static void ConfigureFromVanillaTemplate(SkillTrigger target, CaitlynSkillDefinition definition)
    {
        SkillTrigger template = DewResources.GetByShortTypeName<SkillTrigger>(definition.VanillaTemplateTypeName);
        if (template == null)
        {
            throw new InvalidOperationException(
                $"Required vanilla proxy template {definition.VanillaTemplateTypeName} was not found for {definition.TypeName}.");
        }
        if (template.configs == null || template.configs.Length == 0)
        {
            throw new InvalidOperationException(
                $"Vanilla proxy template {definition.VanillaTemplateTypeName} has no TriggerConfig for {definition.TypeName}.");
        }

        target.rarity = definition.IsIdentity ? Rarity.Identity : Rarity.Unique;
        target.excludeFromPool = true;
        target.type = template.type;
        target.tags = template.tags;
        target.isLevelUpEnabled = template.isLevelUpEnabled;
        target.useCustomSkillHastePerLevel = template.useCustomSkillHastePerLevel;
        target.startEffect = template.startEffect;
        target.endEffect = template.endEffect;
        target.configs = CloneConfigs(template.configs);
    }

    private static TriggerConfig[] CloneConfigs(TriggerConfig[] source)
    {
        TriggerConfig[] clone = new TriggerConfig[source.Length];
        for (int i = 0; i < source.Length; i++)
        {
            if (source[i] == null)
            {
                throw new InvalidOperationException($"Vanilla TriggerConfig at index {i} is null.");
            }
            clone[i] = CaitlynRuntimeClone.DeepClone(source[i]);
        }
        return clone;
    }

    private static void ValidateNoCollisions(DewInternal.DewResourceDatabase database)
    {
        HashSet<string> guids = new(StringComparer.OrdinalIgnoreCase);
        HashSet<Type> runtimeTypes = new();
        HashSet<uint> assetIds = new();

        foreach (CaitlynSkillDefinition definition in CaitlynSkillDefinition.All)
        {
            if (!guids.Add(definition.Guid))
            {
                throw new InvalidOperationException($"Duplicate Caitlyn resource GUID: {definition.Guid}");
            }
            if (!runtimeTypes.Add(definition.RuntimeType))
            {
                throw new InvalidOperationException($"Duplicate Caitlyn runtime type: {definition.RuntimeType.FullName}");
            }

            uint assetId = CaitlynNetworkHelper.GenerateAssetId(definition.Guid);
            if (!assetIds.Add(assetId))
            {
                throw new InvalidOperationException(
                    $"Caitlyn runtime asset-id collision inside this mod: {assetId} ({definition.TypeName}).");
            }

            string aqn = GetAssemblyQualifiedName(definition);
            if (database.typeAssemblyQualifiedNameToGuid.TryGetValue(aqn, out string typeGuid))
            {
                throw new InvalidOperationException(
                    $"Runtime type {definition.RuntimeType.FullName} is already mapped to GUID {typeGuid}.");
            }
            if (database.netObjectAssetIdToGuid.TryGetValue(assetId, out string assetGuid))
            {
                throw new InvalidOperationException(
                    $"Mirror asset id {assetId} for {definition.TypeName} is already mapped to GUID {assetGuid}.");
            }
            if (database.allGuids.Contains(definition.Guid))
            {
                throw new InvalidOperationException(
                    $"Runtime resource GUID {definition.Guid} already exists in DewResourceDatabase.");
            }
        }
    }

    private void VerifyConfigIsolation()
    {
        foreach (CaitlynSkillDefinition definition in CaitlynSkillDefinition.All)
        {
            SkillTrigger target = GetPrefab(definition);
            SkillTrigger template = DewResources.GetByShortTypeName<SkillTrigger>(definition.VanillaTemplateTypeName);
            if (target?.configs == null || template?.configs == null || target.configs.Length != template.configs.Length)
            {
                throw new InvalidOperationException($"TriggerConfig verification failed for {definition.TypeName}.");
            }

            for (int i = 0; i < target.configs.Length; i++)
            {
                TriggerConfig targetConfig = target.configs[i];
                TriggerConfig templateConfig = template.configs[i];
                AssertNotShared(targetConfig, templateConfig, definition.TypeName, i, "TriggerConfig");
                AssertNotShared(targetConfig.castMethod, templateConfig.castMethod, definition.TypeName, i, "castMethod");
                AssertNotShared(targetConfig.channel, templateConfig.channel, definition.TypeName, i, "channel");
                AssertNotShared(targetConfig.predictionSettings, templateConfig.predictionSettings, definition.TypeName, i, "predictionSettings");
                AssertNotShared(targetConfig.selfValidator, templateConfig.selfValidator, definition.TypeName, i, "selfValidator");
                AssertNotShared(targetConfig.targetValidator, templateConfig.targetValidator, definition.TypeName, i, "targetValidator");
            }
        }
    }

    private static void AssertNotShared(
        object target,
        object template,
        string skillName,
        int configIndex,
        string path)
    {
        if (target == null || template == null)
        {
            return;
        }

        Type type = target.GetType();
        if (!type.IsValueType && target is not string && target is not UnityEngine.Object && ReferenceEquals(target, template))
        {
            throw new InvalidOperationException(
                $"Mutable config isolation failed for {skillName} config {configIndex}: {path} is shared with vanilla.");
        }
    }

    private void Rollback(DewInternal.DewResourceDatabase database)
    {
        foreach (RuntimeEntry entry in SnapshotEntries())
        {
            CleanupEntry(entry, database);
        }
        _byGuid.Clear();
        _byType.Clear();
        database.InitForRuntime();
    }

    private void CleanupEntry(RuntimeEntry entry, DewInternal.DewResourceDatabase database)
    {
        if (entry == null)
        {
            return;
        }

        CaitlynNetworkHelper.UnregisterSpawnHandler(entry.AssetId);

        string aqn = entry.Definition.RuntimeType.AssemblyQualifiedName;
        if (!string.IsNullOrEmpty(aqn) &&
            database.typeAssemblyQualifiedNameToGuid.TryGetValue(aqn, out string typeGuid) &&
            string.Equals(typeGuid, entry.Definition.Guid, StringComparison.OrdinalIgnoreCase))
        {
            database.typeAssemblyQualifiedNameToGuid.Remove(aqn);
        }
        if (database.netObjectAssetIdToGuid.TryGetValue(entry.AssetId, out string assetGuid) &&
            string.Equals(assetGuid, entry.Definition.Guid, StringComparison.OrdinalIgnoreCase))
        {
            database.netObjectAssetIdToGuid.Remove(entry.AssetId);
        }
        database.allGuids.Remove(entry.Definition.Guid);
        _byGuid.Remove(entry.Definition.Guid);
        _byType.Remove(entry.Definition.RuntimeType);
        DestroyPrefab(entry);
    }

    private RuntimeEntry[] SnapshotEntries()
    {
        RuntimeEntry[] entries = new RuntimeEntry[_byGuid.Count];
        _byGuid.Values.CopyTo(entries, 0);
        return entries;
    }

    private static void DestroyPrefab(RuntimeEntry entry)
    {
        if (entry?.Prefab != null)
        {
            UnityEngine.Object.DestroyImmediate(entry.Prefab);
        }
    }

    private static string GetAssemblyQualifiedName(CaitlynSkillDefinition definition)
    {
        string aqn = definition?.RuntimeType?.AssemblyQualifiedName;
        if (string.IsNullOrEmpty(aqn))
        {
            throw new InvalidOperationException(
                $"Runtime type {definition?.RuntimeType?.FullName ?? "<null>"} has no assembly-qualified name.");
        }
        return aqn;
    }
}
