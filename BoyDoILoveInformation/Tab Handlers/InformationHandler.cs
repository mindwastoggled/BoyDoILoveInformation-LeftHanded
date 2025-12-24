using System.Linq;
using BoyDoILoveInformation.Tools;
using GorillaLocomotion;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace BoyDoILoveInformation.Tab_Handlers;

public class InformationHandler : TabHandlerBase
{
    public static VRRig       ChosenRig;
    private       TextMeshPro accountCreationDate;
    private       TextMeshPro colourCode;
    private       TextMeshPro fps;

    private float        lastPingUpdate;
    private LineRenderer line;
    private TextMeshPro  ping;
    private TextMeshPro  platform;
    private GameObject   playerHighlighter;

    private TextMeshPro playerName;

    private void Start()
    {
        playerName          = transform.GetChild(0).GetComponent<TextMeshPro>();
        platform            = transform.GetChild(1).GetComponent<TextMeshPro>();
        fps                 = transform.GetChild(2).GetComponent<TextMeshPro>();
        ping                = transform.GetChild(3).GetComponent<TextMeshPro>();
        colourCode          = transform.GetChild(4).GetComponent<TextMeshPro>();
        accountCreationDate = transform.GetChild(5).GetComponent<TextMeshPro>();

        NoPlayerSelected();

        Color mainColourWithWeirdAlpha = new(Plugin.MainColour.r, Plugin.MainColour.g, Plugin.MainColour.b, 0.7f);

        line                = new GameObject("Line").AddComponent<LineRenderer>();
        line.material       = MakeMaterialTransparent(line.material);
        line.material.color = mainColourWithWeirdAlpha;
        line.startWidth     = 0.0125f;
        line.endWidth       = 0.0125f;
        line.positionCount  = 2;
        line.enabled        = true;

        playerHighlighter = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        Destroy(playerHighlighter.GetComponent<Collider>());
        playerHighlighter.GetComponent<Renderer>().material =
                MakeMaterialTransparent(playerHighlighter.GetComponent<Renderer>().material);

        playerHighlighter.GetComponent<Renderer>().material.color = mainColourWithWeirdAlpha;

        playerHighlighter.transform.localScale = Vector3.one * 0.5f;
    }

    private void LateUpdate()
    {
        if (ChosenRig == null)
        {
            Vector3 origin    = BDILIUtils.RealRightController.position;
            Vector3 direction = BDILIUtils.RealRightController.forward;

            if (PhysicsRaycast(origin,      direction,
                        out RaycastHit hit, out VRRig rig))
            {
                line.enabled = true;
                line.SetPosition(0, origin);
                line.SetPosition(1, hit.point);

                if (rig != null)
                {
                    HighlightPlayer(rig);
                    if (ControllerInputPoller.instance.rightControllerIndexFloat > 0.5f ||
                        Mouse.current.leftButton.wasPressedThisFrame)
                    {
                        OnNewRig(rig);
                        ChosenRig = rig;
                    }
                }
                else
                {
                    HighlightPlayer(null);
                }
            }
            else
            {
                line.enabled = false;
                HighlightPlayer(null);
            }
        }
        else
        {
            line.enabled = false;
            int    fpsInt = ChosenRig.fps;
            string colour = fpsInt < 60 ? "red" : fpsInt < 72 ? "yellow" : "green";
            fps.text        = $"<color={colour}>{fpsInt}</color> FPS";
            colourCode.text = ParseIntoColourCode(ChosenRig.playerColor);

            if (lastPingUpdate + 0.1f < Time.time)
            {
                lastPingUpdate = Time.time;
                ping.text      = $"{ChosenRig.GetPing()} ms";
            }

            if (ControllerInputPoller.instance.rightControllerSecondaryButton)
            {
                ChosenRig = null;
                HighlightPlayer(null);
                NoPlayerSelected();
            }
        }
    }

    private void OnEnable()
    {
        if (line != null)
            line.enabled = true;

        OnNewRig(ChosenRig);
        HighlightPlayer(ChosenRig);
    }

    private void OnDisable()
    {
        if (line != null)
            line.enabled = false;

        HighlightPlayer(null);
        NoPlayerSelected();
    }

    private void OnNewRig(VRRig rig)
    {
        if (rig == null)
            return;

        string special = Plugin.HanSoloPlayerIDs.Contains(rig.OwningNetPlayer.UserId) ? " : HanSolo1000Falcon" : "";
        playerName.text          = rig.OwningNetPlayer.SanitizedNickName + special;
        accountCreationDate.text = rig.GetAccountCreationDate().ToString("dd/MM/yyyy");
        platform.text            = rig.GetPlatform().ParsePlatform();
    }

    private void NoPlayerSelected()
    {
        playerName.text          = "No player selected";
        platform.text            = "-";
        fps.text                 = "-";
        ping.text                = "-";
        colourCode.text          = "-";
        accountCreationDate.text = "-";
    }

    private void HighlightPlayer(VRRig rig)
    {
        if (rig == null)
        {
            playerHighlighter.transform.SetParent(null);
            playerHighlighter.SetActive(false);
        }
        else
        {
            playerHighlighter.transform.SetParent(rig.transform);
            playerHighlighter.transform.localPosition = Vector3.zero;
            playerHighlighter.transform.localRotation = Quaternion.identity;
            playerHighlighter.SetActive(true);
        }
    }

    private string ParseIntoColourCode(Color colour)
    {
        int r = Mathf.RoundToInt(colour.r * 9);
        int g = Mathf.RoundToInt(colour.g * 9);
        int b = Mathf.RoundToInt(colour.b * 9);

        return $"<color=red>{r}</color> <color=green>{g}</color> <color=blue>{b}</color>";
    }

    private Material MakeMaterialTransparent(Material material)
    {
        material.shader = Plugin.UberShader;

        material.SetInt("_SrcBlend",      (int)BlendMode.SrcAlpha);
        material.SetInt("_DstBlend",      (int)BlendMode.OneMinusSrcAlpha);
        material.SetInt("_SrcBlendAlpha", (int)BlendMode.One);
        material.SetInt("_DstBlendAlpha", (int)BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite",        0);
        material.SetInt("_AlphaToMask",   0);
        material.renderQueue = (int)RenderQueue.Transparent;

        return material;
    }

    private bool PhysicsRaycast(Vector3 origin, Vector3 direction, out RaycastHit hit, out VRRig rig)
    {
        RaycastHit[] hits = Physics.RaycastAll(origin, direction, 1000f);

        rig = null;
        hit = default(RaycastHit);
        float minDistance = float.MaxValue;

        foreach (RaycastHit hit2 in hits)
            if ((1 << hit2.collider.gameObject.layer & GTPlayer.Instance.locomotionEnabledLayers) != 0
             || hit2.collider.GetComponentInParent<VRRig>() != null &&
                !hit2.collider.GetComponentInParent<VRRig>().isLocal)
                if (hit2.distance < minDistance)
                {
                    minDistance = hit2.distance;
                    hit         = hit2;
                    rig         = hit2.collider.GetComponentInParent<VRRig>();
                }

        return hit.collider != null;
    }
}