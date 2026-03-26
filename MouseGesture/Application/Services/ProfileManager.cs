using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace WindowsFormsApp1
{
    public class ProfileManager
    {
        public List<AppProfile> AppProfiles { get; private set; } = new List<AppProfile>();
        public List<GroupProfile> GroupProfiles { get; private set; } = new List<GroupProfile>();

        // 共通（Global）ジェスチャー用のLiveリスト（UIバインド用）
        public BindingList<GestureMap> GlobalGestureMaps { get; private set; } = new BindingList<GestureMap>();

        public AppProfile CurrentAppProfile { get; private set; }
        public BindingList<GestureMap> CurrentAppGestureMaps { get; private set; } = new BindingList<GestureMap>();

        public GroupProfile CurrentGroupProfile { get; private set; }
        public BindingList<GestureMap> CurrentGroupGestureMaps { get; private set; } = new BindingList<GestureMap>();

        public void SelectAppProfile(AppProfile profile)
        {
            if (CurrentAppProfile != null)
            {
                CurrentAppProfile.Gestures = CurrentAppGestureMaps.ToList();
            }

            CurrentAppProfile = profile;
            CurrentAppGestureMaps.Clear();

            if (profile != null && profile.Gestures != null)
            {
                foreach (var g in profile.Gestures) CurrentAppGestureMaps.Add(g);
            }
        }

        public void SelectGroupProfile(GroupProfile profile)
        {
            if (CurrentGroupProfile != null)
            {
                CurrentGroupProfile.Gestures = CurrentGroupGestureMaps.ToList();
            }

            CurrentGroupProfile = profile;
            CurrentGroupGestureMaps.Clear();

            if (profile != null && profile.Gestures != null)
            {
                foreach (var g in profile.Gestures) CurrentGroupGestureMaps.Add(g);
            }
        }

        private readonly string _settingsFilePath;

        public ProfileManager(string settingsFilePath)
        {
            _settingsFilePath = settingsFilePath;
        }

        public void LoadGestures()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    string rootName = "";
                    using (System.Xml.XmlReader reader = System.Xml.XmlReader.Create(_settingsFilePath))
                    {
                        if (reader.MoveToContent() == System.Xml.XmlNodeType.Element)
                        {
                            rootName = reader.Name;
                        }
                    }

                    if (rootName == "GestureConfig")
                    {
                        XmlSerializer serializer = new XmlSerializer(typeof(GestureConfig));
                        using (FileStream fs = new FileStream(_settingsFilePath, FileMode.Open))
                        {
                            var config = (GestureConfig)serializer.Deserialize(fs);
                            AppProfiles = config.AppProfiles ?? new List<AppProfile>();
                            GroupProfiles = config.GroupProfiles ?? new List<GroupProfile>();
                        }
                    }
                    else if (rootName == "ArrayOfAppProfile")
                    {
                        XmlSerializer serializer = new XmlSerializer(typeof(List<AppProfile>));
                        using (FileStream fs = new FileStream(_settingsFilePath, FileMode.Open))
                        {
                            AppProfiles = (List<AppProfile>)serializer.Deserialize(fs);
                        }
                        GroupProfiles = new List<GroupProfile>();
                    }
                    else
                    {
                        // Fallback (old format)
                        XmlSerializer serializer = new XmlSerializer(typeof(List<GestureMap>));
                        using (FileStream fs = new FileStream(_settingsFilePath, FileMode.Open))
                        {
                            var loadedGestures = (List<GestureMap>)serializer.Deserialize(fs);
                            AppProfiles.Clear();
                            AppProfiles.Add(new AppProfile { AppName = "Global", Gestures = loadedGestures });
                        }
                        GroupProfiles = new List<GroupProfile>();
                    }
                }
            }
            catch (Exception ex)
            {
                throw new ApplicationException($"設定の読み込みに失敗しました: {ex.Message}", ex);
            }

            // Ensure Global profile exists
            if (AppProfiles.Count == 0 || !AppProfiles.Any(p => string.IsNullOrEmpty(p.AppPath)))
            {
                var global = AppProfiles.FirstOrDefault(p => string.IsNullOrEmpty(p.AppPath));
                if (global == null)
                {
                    global = new AppProfile { AppName = "Global", AppPath = "" };
                    AppProfiles.Insert(0, global);
                }
            }

            // Populate Main Global Binding List
            var globalProfile = AppProfiles.First(p => string.IsNullOrEmpty(p.AppPath));
            GlobalGestureMaps.Clear();
            foreach (var g in globalProfile.Gestures) GlobalGestureMaps.Add(g);

            RebuildDictionaries();
        }

        public void SaveGestures()
        {
            try
            {
                // Sync Global
                var globalProfile = AppProfiles.First(p => string.IsNullOrEmpty(p.AppPath));
                globalProfile.Gestures = GlobalGestureMaps.ToList();

                // Sync Current App
                if (CurrentAppProfile != null)
                {
                    CurrentAppProfile.Gestures = CurrentAppGestureMaps.ToList();
                }

                // Sync Current Group
                if (CurrentGroupProfile != null)
                {
                    CurrentGroupProfile.Gestures = CurrentGroupGestureMaps.ToList();
                }

                RebuildDictionaries();

                GestureConfig config = new GestureConfig
                {
                    AppProfiles = AppProfiles,
                    GroupProfiles = GroupProfiles
                };

                XmlSerializer serializer = new XmlSerializer(typeof(GestureConfig));
                using (FileStream fs = new FileStream(_settingsFilePath, FileMode.Create))
                {
                    serializer.Serialize(fs, config);
                }
            }
            catch (Exception ex)
            {
                throw new ApplicationException($"設定の保存に失敗しました: {ex.Message}", ex);
            }
        }

        private void RebuildDictionaries()
        {
            foreach (var profile in AppProfiles)
            {
                profile.RebuildDictionary();
            }
            foreach (var group in GroupProfiles)
            {
                group.RebuildDictionary();
            }
        }

        public GestureMap FindGestureMap(string gesture, string appPath)
        {
            AppProfile appProfile = null;

            if (!string.IsNullOrEmpty(appPath))
            {
                // 1. Try exact path match
                for (int i = 0; i < AppProfiles.Count; i++)
                {
                    var p = AppProfiles[i];
                    if (!string.IsNullOrEmpty(p.AppPath) && appPath.Equals(p.AppPath, StringComparison.OrdinalIgnoreCase))
                    {
                        appProfile = p;
                        break;
                    }
                }

                // 2. Try file name match
                if (appProfile == null)
                {
                    try
                    {
                        string appName = Path.GetFileName(appPath);
                        for (int i = 0; i < AppProfiles.Count; i++)
                        {
                            var p = AppProfiles[i];
                            if (!string.IsNullOrEmpty(p.AppPath) && string.Equals(Path.GetFileName(p.AppPath), appName, StringComparison.OrdinalIgnoreCase))
                            {
                                appProfile = p;
                                break;
                            }
                        }
                    }
                    catch { }
                }

                // 無効化されているアプリの場合は処理しない（MainForm等でガードされていないときのため）
                if (appProfile != null && appProfile.Disabled)
                {
                    return null;
                }
            }

            GestureMap map = null;

            // Priority 1: App Specific
            if (appProfile != null)
            {
                if (CurrentAppProfile != null && appProfile == CurrentAppProfile)
                {
                    for (int i = 0; i < CurrentAppGestureMaps.Count; i++)
                    {
                        if (CurrentAppGestureMaps[i].Gesture == gesture)
                        {
                            map = CurrentAppGestureMaps[i];
                            break;
                        }
                    }
                }
                else
                {
                    appProfile.GestureDict.TryGetValue(gesture, out map);
                }
            }

            // Priority 2: Group Specific
            if (map == null && !string.IsNullOrEmpty(appPath))
            {
                for (int i = 0; i < GroupProfiles.Count; i++)
                {
                    var group = GroupProfiles[i];
                    bool inGroup = false;
                    for (int j = 0; j < group.TargetAppPaths.Count; j++)
                    {
                        if (group.TargetAppPaths[j].Equals(appPath, StringComparison.OrdinalIgnoreCase))
                        {
                            inGroup = true;
                            break;
                        }
                    }

                    if (!inGroup && appProfile != null && !string.IsNullOrEmpty(appProfile.AppPath))
                    {
                        for (int j = 0; j < group.TargetAppPaths.Count; j++)
                        {
                            if (group.TargetAppPaths[j].Equals(appProfile.AppPath, StringComparison.OrdinalIgnoreCase))
                            {
                                inGroup = true;
                                break;
                            }
                        }
                    }

                    if (inGroup)
                    {
                        if (CurrentGroupProfile != null && group == CurrentGroupProfile)
                        {
                            for (int j = 0; j < CurrentGroupGestureMaps.Count; j++)
                            {
                                if (CurrentGroupGestureMaps[j].Gesture == gesture)
                                {
                                    map = CurrentGroupGestureMaps[j];
                                    break;
                                }
                            }
                        }
                        else
                        {
                            group.GestureDict.TryGetValue(gesture, out map);
                        }

                        if (map != null) break;
                    }
                }
            }

            // Priority 3: Global
            if (map == null)
            {
                AppProfile globalProfile = null;
                for (int i = 0; i < AppProfiles.Count; i++)
                {
                    if (string.IsNullOrEmpty(AppProfiles[i].AppPath))
                    {
                        globalProfile = AppProfiles[i];
                        break;
                    }
                }

                if (globalProfile != null && globalProfile.GestureDict.TryGetValue(gesture, out GestureMap gm))
                {
                    map = gm;
                }

                if (map == null)
                {
                    for (int i = 0; i < GlobalGestureMaps.Count; i++)
                    {
                        if (GlobalGestureMaps[i].Gesture == gesture)
                        {
                            map = GlobalGestureMaps[i];
                            break;
                        }
                    }
                }
            }

            return map;
        }

        public bool IsAppDisabled(string appPath)
        {
            if (string.IsNullOrEmpty(appPath)) return false;

            AppProfile profile = null;
            for (int i = 0; i < AppProfiles.Count; i++)
            {
                var p = AppProfiles[i];
                if (!string.IsNullOrEmpty(p.AppPath) && appPath.Equals(p.AppPath, StringComparison.OrdinalIgnoreCase))
                {
                    profile = p;
                    break;
                }
            }

            if (profile == null)
            {
                try
                {
                    string appName = Path.GetFileName(appPath);
                    for (int i = 0; i < AppProfiles.Count; i++)
                    {
                        var p = AppProfiles[i];
                        if (!string.IsNullOrEmpty(p.AppPath) && string.Equals(Path.GetFileName(p.AppPath), appName, StringComparison.OrdinalIgnoreCase))
                        {
                            profile = p;
                            break;
                        }
                    }
                }
                catch { }
            }
            return profile != null && profile.Disabled;
        }
    }
}
