using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Photon.Pun;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace CustomColors
{
    [BepInPlugin(modGUID, modName, modVersion)]
    public class CustomColorsMod : BaseUnityPlugin
    {
        private const string modGUID = "x753.CustomColors";
        private const string modName = "CustomColors";
        private const string modVersion = "1.0.0";

        private readonly Harmony harmony = new Harmony(modGUID);

        private static CustomColorsMod Instance;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }

            harmony.PatchAll();
            Logger.LogInfo($"Plugin {modName} is loaded!");

            // Attempt to load a color from your save file!
            ES3Settings es3Settings = new ES3Settings("ModSettingsData.es3", new Enum[]
            {
                    ES3.Location.File
            });
            if (ES3.KeyExists("PlayerBodyColorR", es3Settings))
            {
                string savedR = ES3.Load<string>("PlayerBodyColorR", defaultValue: "0", es3Settings);
                string savedG = ES3.Load<string>("PlayerBodyColorG", defaultValue: "1", es3Settings);
                string savedB = ES3.Load<string>("PlayerBodyColorB", defaultValue: "0", es3Settings);
                ModdedColorPlayerAvatar.LocalColor.r = float.Parse(savedR);
                ModdedColorPlayerAvatar.LocalColor.g = float.Parse(savedG);
                ModdedColorPlayerAvatar.LocalColor.b = float.Parse(savedB);
            }
        }

        // ==============================================================================
        // ModdedColorPlayerAvatar behaviour to handle color changing and networking
        // ==============================================================================
        [HarmonyPatch(typeof(PlayerAvatar), "Awake")]
        class PlayerAvatar_Awake_Patch
        {
            [HarmonyPostfix]
            public static void PlayerAvatar_Awake_Postfix(PlayerAvatar __instance)
            {
                ModdedColorPlayerAvatar moddedColorPlayerAvatar = __instance.gameObject.AddComponent<ModdedColorPlayerAvatar>();
            }
        }

        public class ModdedColorPlayerAvatar : MonoBehaviour, IPunObservable
        {
            public Color moddedColor;
            public static Color LocalColor = new Color(1f, 0f, 0f);

            public PlayerAvatar avatar;
            public PlayerAvatarVisuals visuals;
            public Material bodyMaterial;

            static FieldInfo BodyMaterial = typeof(PlayerHealth).GetField("bodyMaterial", BindingFlags.NonPublic | BindingFlags.Instance);
            static FieldInfo VisualsColor = typeof(PlayerAvatarVisuals).GetField("color", BindingFlags.NonPublic | BindingFlags.Instance);
            static FieldInfo ColorSet = typeof(PlayerAvatarVisuals).GetField("colorSet", BindingFlags.NonPublic | BindingFlags.Instance);
            static FieldInfo MenuPlayerListedList = typeof(MenuPageLobby).GetField("menuPlayerListedList", BindingFlags.NonPublic | BindingFlags.Instance);
            static FieldInfo MenuPlayerListed_PlayerAvatar = typeof(MenuPlayerListed).GetField("playerAvatar", BindingFlags.NonPublic | BindingFlags.Instance);

            public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
            {
                throw new NotImplementedException();
            }

            void Awake()
            {
                avatar = gameObject.GetComponent<PlayerAvatar>();
                visuals = avatar.playerAvatarVisuals;
            }

            public static void LocalPlayerAvatarSetColor()
            {
                if (!GameManager.Multiplayer())
                {
                    PlayerAvatar.instance.GetComponent<ModdedColorPlayerAvatar>().ModdedSetColorRPC(LocalColor.r, LocalColor.g, LocalColor.b);
                }
                else
                {
                    PlayerAvatar.instance.photonView.RPC("ModdedSetColorRPC", RpcTarget.AllBuffered, new object[]
                    {
                        LocalColor.r, LocalColor.g, LocalColor.b
                    });
                }

                ES3Settings es3Settings = new ES3Settings("ModSettingsData.es3", new Enum[]
                {
                    ES3.Location.File
                });
                ES3.Save<string>("PlayerBodyColorR", ModdedColorPlayerAvatar.LocalColor.r.ToString(), es3Settings);
                ES3.Save<string>("PlayerBodyColorG", ModdedColorPlayerAvatar.LocalColor.g.ToString(), es3Settings);
                ES3.Save<string>("PlayerBodyColorB", ModdedColorPlayerAvatar.LocalColor.b.ToString(), es3Settings);
            }

            [PunRPC]
            public void ModdedSetColorRPC(float r, float g, float b)
            {
                //Debug.Log($"ModdedSetColorRPC called {r}, {g}, {b}");

                Color color = new Color(r, g, b);
                moddedColor = color;

                VisualsColor.SetValue(visuals, color);

                if (bodyMaterial == null)
                {
                    bodyMaterial = (Material)BodyMaterial.GetValue(avatar.playerHealth);
                }

                bodyMaterial.SetColor(Shader.PropertyToID("_AlbedoColor"), color);

                if (SemiFunc.RunIsLobbyMenu() && MenuPageLobby.instance)
                {
                    List<MenuPlayerListed> menuPlayerListedList = (List<MenuPlayerListed>)MenuPlayerListedList.GetValue(MenuPageLobby.instance);
                    foreach (MenuPlayerListed menuPlayerListed in menuPlayerListedList)
                    {
                        if ((PlayerAvatar) MenuPlayerListed_PlayerAvatar.GetValue(menuPlayerListed) == avatar)
                        {
                            menuPlayerListed.playerHead.SetColor(color);
                            break;
                        }
                    }
                }

                ColorSet.SetValue(visuals, true);
            }
        }

        // ==============================================================================
        // Modifying MenuPageColor with custom MenuSliders that have custom MenuSettings
        // ==============================================================================
        public static MenuSlider RedSlider;
        public static MenuSlider GreenSlider;
        public static MenuSlider BlueSlider;

        public class SliderReinitializer : MonoBehaviour
        {
            public string elementName = "";
            public MenuSlider menuSlider;
            public float startValue = 0f;

            public void Start()
            {
                this.gameObject.GetComponent<MenuSlider>().elementName = this.elementName;
                this.gameObject.transform.Find("Element Name").GetComponent<TextMeshProUGUI>().SetText(this.elementName);
                this.gameObject.name = "Slider - " + elementName;
                this.menuSlider.SetBar(this.startValue);
            }
        }

        public static MenuSlider CreateColorSlider(GameObject sliderPrefab, Transform parentTransform, Vector3 position, string elementName, Color color, int settingNum)
        {
            GameObject slider = GameObject.Instantiate(sliderPrefab, parentTransform);

            slider.transform.position = position;
            slider.transform.Find("SliderBG").Find("RawImage (1)").GetComponent<RawImage>().color = Color.black;
            slider.transform.Find("SliderBG").Find("RawImage (2)").gameObject.SetActive(false);
            slider.transform.Find("SliderBG").Find("RawImage (3)").GetComponent<RawImage>().color = new Color(0.1f, 0.1f, 0.1f);
            slider.transform.Find("MaskedText").gameObject.SetActive(false);
            slider.transform.Find("Bar").Find("RawImage").GetComponent<RawImage>().color = color;

            MenuSlider menuSlider = slider.GetComponent<MenuSlider>();
            menuSlider.pointerSegmentJump = 1;
            menuSlider.buttonSegmentJump = 1;

            slider.GetComponent<MenuSetting>().setting = (DataDirector.Setting)settingNum;
            Destroy(slider.GetComponent<MenuSettingElement>());

            // We need to run some code after MenuSlider's Start() so it isn't overwritten
            SliderReinitializer reinitializer = slider.AddComponent<SliderReinitializer>();
            reinitializer.menuSlider = menuSlider;
            reinitializer.elementName = elementName;
            if(color == Color.red)
            {
                reinitializer.startValue = ModdedColorPlayerAvatar.LocalColor.r;
            }
            else if (color == Color.green)
            {
                reinitializer.startValue = ModdedColorPlayerAvatar.LocalColor.g;
            }
            else if (color == Color.blue)
            {
                reinitializer.startValue = ModdedColorPlayerAvatar.LocalColor.b;
            }

            return menuSlider;
        }

        [HarmonyPatch(typeof(MenuPageColor), "Start")]
        class MenuPageColor_Start_Patch
        {
            [HarmonyPostfix]
            public static void MenuPageColor_Start_Postfix(MenuPageColor __instance)
            {
                Transform oldColors = __instance.transform.Find("Color Button Holder");

                __instance.transform.Find("Menu Button - Confirm").localPosition = new Vector3(330f, 80f, 0f);

                foreach (MenuManager.MenuPages menuPages in MenuManager.instance.menuPages)
                {
                    GameObject page = menuPages.menuPage;
                    if (page.name == "Menu Page Settings Audio")
                    {
                        Transform scrollBox = page.transform.Find("Menu Scroll Box");
                        Transform mask = scrollBox?.Find("Mask");
                        Transform scroller = mask?.Find("Scroller");
                        Transform slider = scroller?.Find("Slider - Master volume");

                        if (slider != null)
                        {
                            RedSlider = CreateColorSlider(slider.gameObject, __instance.transform, new Vector3(60f, 525f, 0f), "Color [RED]", Color.red, 753);
                            GreenSlider = CreateColorSlider(slider.gameObject, __instance.transform, new Vector3(60f, 495f, 0f), "Color [GREEN]", Color.green, 754);
                            BlueSlider = CreateColorSlider(slider.gameObject, __instance.transform, new Vector3(60f, 465f, 0f), "Color [BLUE]", Color.blue, 755);
                        }
                    }
                }
            }
        }

        //[HarmonyPatch(typeof(MenuButtonColor), "Start")]
        //class MenuButtonColor_Start_Patch
        //{
        //    static FieldInfo ColorID = typeof(MenuButtonColor).GetField("colorID", BindingFlags.NonPublic | BindingFlags.Instance);

        //    [HarmonyPostfix]
        //    public static void MenuButtonColor_Start_Postfix(MenuButtonColor __instance)
        //    {
        //        if(DataDirector.instance.ColorGetBody() == (int)ColorID.GetValue(__instance))
        //        {
        //            // Move the selecter to it
        //        }
        //    }
        //}

        [HarmonyPatch(typeof(PlayerAvatar), "PlayerAvatarSetColor")]
        class PlayerAvatar_PlayerAvatarSetColor_Patch
        {
            [HarmonyPostfix]
            public static void PlayerAvatar_PlayerAvatarSetColor_Postfix(PlayerAvatar __instance, int colorIndex)
            {
                // If they have a modded color associated with them, use that!
                if (__instance.GetComponent<ModdedColorPlayerAvatar>()?.moddedColor != null)
                {
                    ModdedColorPlayerAvatar.LocalPlayerAvatarSetColor();
                }
            }
        }

        [HarmonyPatch(typeof(PlayerAvatarVisuals), "MenuAvatarGetColorsFromRealAvatar")]
        class PlayerAvatarVisuals_MenuAvatarGetColorsFromRealAvatar_Patch
        {
            public static Material bodyMaterial;

            static FieldInfo BodyMaterial = typeof(PlayerHealth).GetField("bodyMaterial", BindingFlags.NonPublic | BindingFlags.Instance);

            [HarmonyPrefix]
            public static bool PlayerAvatarVisuals_MenuAvatarGetColorsFromRealAvatar_Prefix(PlayerAvatarVisuals __instance)
            {
                if (__instance.isMenuAvatar && !__instance.playerAvatar)
                {
                    __instance.playerAvatar = PlayerAvatar.instance;
                }

                bodyMaterial = (Material)BodyMaterial.GetValue(__instance.transform.GetComponentInParent<PlayerHealth>());
                bodyMaterial.SetColor(Shader.PropertyToID("_AlbedoColor"), ModdedColorPlayerAvatar.LocalColor);

                return false;
            }
        }

        [HarmonyPatch(typeof(MenuColorSelected), "SetColor")]
        class MenuColorSelected_SetColor_Patch
        {

            [HarmonyPostfix]
            public static void MenuColorSelected_SetColor_Postfix(MenuColorSelected __instance, Color color, Vector3 position)
            {
                if (RedSlider != null)
                {
                    RedSlider.SetBar(color.r);
                }
                if (GreenSlider != null)
                {
                    GreenSlider.SetBar(color.g);
                }
                if (BlueSlider != null)
                {
                    BlueSlider.SetBar(color.b);
                }

                ModdedColorPlayerAvatar.LocalColor = color;
            }
        }

        [HarmonyPatch(typeof(MenuPageColor), "ConfirmButton")]
        class MenuPageColor_ConfirmButton_Patch
        {
            [HarmonyPrefix]
            public static void MenuPageColor_ConfirmButton_Prefix(MenuPageColor __instance)
            {
                ModdedColorPlayerAvatar.LocalPlayerAvatarSetColor();
            }
        }

        [HarmonyPatch(typeof(DataDirector), "SettingValueSet")]
        class DataDirector_SettingValueSet_Patch
        {
            [HarmonyPrefix]
            public static bool DataDirector_SettingValueSet_Prefix(DataDirector __instance, DataDirector.Setting setting, int value)
            {
                if (setting == (DataDirector.Setting)753)
                {
                    ModdedColorPlayerAvatar.LocalColor.r = value / 100f;
                }
                if (setting == (DataDirector.Setting)754)
                {
                    ModdedColorPlayerAvatar.LocalColor.g = value / 100f;
                }
                if (setting == (DataDirector.Setting)755)
                {
                    ModdedColorPlayerAvatar.LocalColor.b = value / 100f;
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(DataDirector), "InitializeSettings")]
        class DataDirector_InitializeSettings_Patch
        {
            static MethodInfo SettingAdd = typeof(DataDirector).GetMethod("SettingAdd", BindingFlags.NonPublic | BindingFlags.Instance);

            [HarmonyPostfix]
            public static void DataDirector_InitializeSettings_Postfix(DataDirector __instance)
            {
                // Potentially remove all this?
                SettingAdd.Invoke(__instance, [DataDirector.SettingType.None, (DataDirector.Setting)753, "Modded_CustomColor_R", 50]);
                SettingAdd.Invoke(__instance, [DataDirector.SettingType.None, (DataDirector.Setting)754, "Modded_CustomColor_G", 50]);
                SettingAdd.Invoke(__instance, [DataDirector.SettingType.None, (DataDirector.Setting)755, "Modded_CustomColor_B", 50]);
            }
        }
    }
}