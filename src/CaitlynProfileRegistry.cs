using System;
using System.Collections.Generic;

namespace SodLolCaitlyn;

/// <summary>
/// Exposes custom Memories to the running game while keeping mod-owned profile
/// and statistics keys out of serialized player saves.
/// </summary>
internal sealed class CaitlynProfileRegistry
{
    private readonly HashSet<string> _addedContentArray = new(StringComparer.Ordinal);
    private readonly HashSet<string> _addedContentList = new(StringComparer.Ordinal);
    private readonly HashSet<string> _ownedProfileSkills = new(StringComparer.Ordinal);
    private readonly HashSet<string> _ownedDejavuKeys = new(StringComparer.Ordinal);
    private readonly HashSet<string> _ownedProfileStats = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _dejavuRuntimeValues = new(StringComparer.Ordinal);

    private DewGameContentSettings _content;
    private DewProfile _profile;
    private DewProfileStats _stats;

    private bool _registered;
    private int _saveSuspendDepth;

    public void RegisterAll()
    {
        if (_registered)
        {
            return;
        }

        try
        {
            if (DewSave.onSaveStarted == null || DewSave.onSaveEnded == null)
            {
                throw new InvalidOperationException(
                    "DewSave save lifecycle events are unavailable; refusing to register runtime profile keys that could leak into the save file.");
            }

            _content = DewBuildProfile.current?.content;
            _profile = DewSave.profileMain;
            _stats = DewSave.profileStats;

            RegisterContentSettings();
            RegisterPersistentRuntimeEntries(initialRegistration: true);

            DewSave.onSaveStarted.Add(OnSaveStarted);
            DewSave.onSaveEnded.Add(OnSaveEnded);
            _registered = true;
        }
        catch
        {
            UnregisterAll();
            throw;
        }
    }

    public void UnregisterAll()
    {
        // Remove is intentionally attempted even after partial registration.
        DewSave.onSaveStarted?.Remove(OnSaveStarted);
        DewSave.onSaveEnded?.Remove(OnSaveEnded);

        RemovePersistentRuntimeEntries(captureDejavuValues: false);
        RestoreContentSettings();

        _registered = false;
        _saveSuspendDepth = 0;
        _ownedProfileSkills.Clear();
        _ownedDejavuKeys.Clear();
        _ownedProfileStats.Clear();
        _dejavuRuntimeValues.Clear();
        _content = null;
        _profile = null;
        _stats = null;
    }

    private void RegisterContentSettings()
    {
        if (_content == null)
        {
            return;
        }

        bool hasExplicitSkillList =
            (_content._availableSkills != null && _content._availableSkills.Length > 0) ||
            (_content.availableSkills != null && _content.availableSkills.Count > 0);
        if (!hasExplicitSkillList)
        {
            return;
        }

        foreach (CaitlynSkillDefinition definition in CaitlynSkillDefinition.All)
        {
            string key = definition.TypeName;

            if (_content._availableSkills != null && !Contains(_content._availableSkills, key))
            {
                string[] oldValues = _content._availableSkills;
                string[] newValues = new string[oldValues.Length + 1];
                Array.Copy(oldValues, newValues, oldValues.Length);
                newValues[oldValues.Length] = key;
                _content._availableSkills = newValues;
                _addedContentArray.Add(key);
            }

            if (_content.availableSkills != null && !_content.availableSkills.Contains(key))
            {
                _content.availableSkills.Add(key);
                _addedContentList.Add(key);
            }
        }
    }

    private void RegisterPersistentRuntimeEntries(bool initialRegistration)
    {
        foreach (CaitlynSkillDefinition definition in CaitlynSkillDefinition.All)
        {
            string key = definition.TypeName;
            EnsureProfileSkill(key, initialRegistration);
            EnsureDejavuKey(key, initialRegistration);
            EnsureProfileStat(key, initialRegistration);
        }
    }

    private void EnsureProfileSkill(string key, bool initialRegistration)
    {
        if (_profile?.skills == null)
        {
            return;
        }

        if (_profile.skills.ContainsKey(key))
        {
            // During the save window our own key was removed. If another system
            // creates the same key before onSaveEnded, relinquish ownership so
            // unload cannot delete another system's value.
            if (!initialRegistration && _ownedProfileSkills.Contains(key))
            {
                _ownedProfileSkills.Remove(key);
            }
            return;
        }

        if (initialRegistration || _ownedProfileSkills.Contains(key))
        {
            _profile.skills.Add(key, new DewProfile.UnlockData
            {
                status = UnlockStatus.Complete,
                didReadMemory = true,
                isNewHeroOrHeroSkill = false
            });
            _ownedProfileSkills.Add(key);
        }
    }

    private void EnsureDejavuKey(string key, bool initialRegistration)
    {
        if (_profile?.dejavuCostReductionPeriodTimestamp == null)
        {
            return;
        }

        if (_profile.dejavuCostReductionPeriodTimestamp.ContainsKey(key))
        {
            if (!initialRegistration && _ownedDejavuKeys.Contains(key))
            {
                _ownedDejavuKeys.Remove(key);
                _dejavuRuntimeValues.Remove(key);
            }
            return;
        }

        if (initialRegistration)
        {
            _ownedDejavuKeys.Add(key);
            _dejavuRuntimeValues[key] = 0L;
        }

        if (_ownedDejavuKeys.Contains(key))
        {
            long value = _dejavuRuntimeValues.TryGetValue(key, out long runtimeValue)
                ? runtimeValue
                : 0L;
            _profile.dejavuCostReductionPeriodTimestamp.Add(key, value);
        }
    }

    private void EnsureProfileStat(string key, bool initialRegistration)
    {
        if (_stats?.skills == null)
        {
            return;
        }

        if (_stats.skills.ContainsKey(key))
        {
            if (!initialRegistration && _ownedProfileStats.Contains(key))
            {
                _ownedProfileStats.Remove(key);
            }
            return;
        }

        if (initialRegistration || _ownedProfileStats.Contains(key))
        {
            _stats.skills.Add(key, new DewProfileStats.ItemData());
            _ownedProfileStats.Add(key);
        }
    }

    private void OnSaveStarted()
    {
        _saveSuspendDepth++;
        if (_saveSuspendDepth == 1)
        {
            RemovePersistentRuntimeEntries(captureDejavuValues: true);
        }
    }

    private void OnSaveEnded()
    {
        if (_saveSuspendDepth <= 0)
        {
            _saveSuspendDepth = 0;
            return;
        }

        _saveSuspendDepth--;
        if (_saveSuspendDepth == 0 && _registered)
        {
            RegisterPersistentRuntimeEntries(initialRegistration: false);
        }
    }

    private void RemovePersistentRuntimeEntries(bool captureDejavuValues)
    {
        if (_profile?.skills != null)
        {
            foreach (string key in _ownedProfileSkills)
            {
                _profile.skills.Remove(key);
            }
        }

        if (_profile?.dejavuCostReductionPeriodTimestamp != null)
        {
            foreach (string key in _ownedDejavuKeys)
            {
                if (captureDejavuValues &&
                    _profile.dejavuCostReductionPeriodTimestamp.TryGetValue(key, out long current))
                {
                    _dejavuRuntimeValues[key] = current;
                }

                _profile.dejavuCostReductionPeriodTimestamp.Remove(key);
            }
        }

        if (_stats?.skills != null)
        {
            foreach (string key in _ownedProfileStats)
            {
                _stats.skills.Remove(key);
            }
        }
    }

    private void RestoreContentSettings()
    {
        if (_content != null)
        {
            if (_content._availableSkills != null && _addedContentArray.Count > 0)
            {
                List<string> kept = new(_content._availableSkills.Length);
                foreach (string value in _content._availableSkills)
                {
                    if (!_addedContentArray.Contains(value))
                    {
                        kept.Add(value);
                    }
                }
                _content._availableSkills = kept.ToArray();
            }

            if (_content.availableSkills != null)
            {
                foreach (string value in _addedContentList)
                {
                    while (_content.availableSkills.Remove(value))
                    {
                    }
                }
            }
        }

        _addedContentArray.Clear();
        _addedContentList.Clear();
    }

    private static bool Contains(string[] values, string target)
    {
        foreach (string value in values)
        {
            if (value == target)
            {
                return true;
            }
        }
        return false;
    }
}
