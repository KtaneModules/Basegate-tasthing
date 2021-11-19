using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KModkit;
using System.Text.RegularExpressions;
using rnd = UnityEngine.Random;

public class basegate : MonoBehaviour
{
    public new KMAudio audio;
    public KMBombInfo bomb;
    public KMBombModule module;

    public KMSelectable submitButton;
    public KMSelectable[] hatchCovers;
    public KMSelectable[] dials;
    public KMSelectable[] switches;
    private Renderer[] switchRenders;
    private Renderer[] switchSockets;
    public TextMesh[] colorblindTexts;
    private Transform[] dialTransforms;
    private GameObject[] hatchHighlights;
    public Color[] switchColors;
    public Color[] socketColors;

    private int[] switchColorIndices = new int[4];
    private int[] socketColorIndices = new int[4];
    private int calculatedColor;
    private int calculatedNumber;
    private bool[] switchStates = new bool[4];
    private bool[] correctSwitchStates = new bool[4];
    private int[] kValues = new int[3];
    private bool[] correctHatchPositions = new bool[9];
    private bool[] hatchesOpened = new bool[9];
    private int[] hatchIndices = new int[3];
    private int[] dialPositions = new int[9];
    private int[] correctDialPositions = new int[3];

    private static int[] colorTable;
    private static readonly string[] colorNames = new[] { "red", "green", "blue", "yellow", "cyan", "magenta", "white" };
    private static readonly string[] hatchDirections = new[] { "top left", "top middle", "top right", "middle left", "middle middle", "middle right", "bottom left", "bottom middle", "bottom right" };

    private Coroutine[] switchMovements = new Coroutine[4];
    private Coroutine[] dialMovements = new Coroutine[9];

    private static int moduleIdCounter = 1;
    private int moduleId;
    private bool moduleSolved;

    private void Awake()
    {
        moduleId = moduleIdCounter++;
        switchRenders = switches.Select(x => x.GetComponent<Renderer>()).ToArray();
        switchSockets = switches.Select(x => x.transform.Find("socket").GetComponent<Renderer>()).ToArray();
        dialTransforms = dials.Select(x => x.transform.Find("dial")).ToArray();
        hatchHighlights = hatchCovers.Select(x => x.transform.Find("hl").gameObject).ToArray();
        colorTable = "RYWGMCBYGCRBWMWCBMGYRGRMYWBCMBGWCRYCWYBRMGBMRCYGW".Select(x => "RGBYCMW".IndexOf(x)).ToArray();

        foreach (GameObject colorblindText in colorblindTexts.Select(t => t.gameObject))
            colorblindText.SetActive(GetComponent<KMColorblindMode>().ColorblindModeActive);
        submitButton.OnInteract += delegate () { PressSubmitButton(); return false; };
        foreach (KMSelectable bigSwitch in switches)
            bigSwitch.OnInteract += delegate () { FlipSwitch(bigSwitch); return false; };
        foreach (KMSelectable hatch in hatchCovers)
            hatch.OnInteract += delegate () { PressHatch(hatch); return false; };
        foreach (KMSelectable dial in dials)
            dial.OnInteract += delegate () { PressDial(dial); return false; };
    }

    private void Start()
    {
        StartCoroutine(DisableStuff());
        kValues = new[] { bomb.GetBatteryCount(), bomb.GetIndicators().Count(), bomb.GetPortCount() };
        for (int i = 0; i < 4; i++)
        {
            switchColorIndices[i] = rnd.Range(0, 7);
            socketColorIndices[i] = rnd.Range(0, 7);
            switchRenders[i].material.color = switchColors[switchColorIndices[i]];
            switchSockets[i].material.color = socketColors[socketColorIndices[i]];
            colorblindTexts[i].text = "RGBYCMW"[switchColorIndices[i]].ToString() + "RGBYCMW"[socketColorIndices[i]];
            switchStates[i] = rnd.Range(0, 2) == 0;
            switches[i].transform.localEulerAngles = new Vector3(switchStates[i] ? 55f : -55f, i % 2 == 0 ? 90f : 0f, 0f);
        }
        Debug.LogFormat("[Basegate #{0}] Switch colors (clockwise): {1}.", moduleId, switchColorIndices.Select(x => colorNames[x]).Join(", "));
        Debug.LogFormat("[Basegate #{0}] Socket colors (clockwise): {1}.", moduleId, socketColorIndices.Select(x => colorNames[x]).Join(", "));

        var colorsObtained = new List<int>();
        var setNames = new[] { "the top and bottom switches", "the left and right switches", "the two colors from the previous steps" };
        for (int i = 0; i < 3; i++)
        {
            var k = kValues[i];
            var using1 = i == 0 ? switchColorIndices[0] : i == 1 ? switchColorIndices[3] : colorTable[colorsObtained[0]]; // Row
            var using2 = i == 0 ? switchColorIndices[2] : i == 1 ? switchColorIndices[1] : colorTable[colorsObtained[1]]; // Color to find in row
            var using3 = i == 0 ? socketColorIndices[0] : i == 1 ? socketColorIndices[1] : (bomb.GetSolvableModuleNames().Count() % 2 == 0 ? socketColorIndices[2] : socketColorIndices[3]); // Position of the socket in the row, to be modified
            Debug.LogFormat("[Basegate #{0}] Using {1}: In the {2} row, look for {3} to get a modification. Also look for {4}, go forwards {5} time{6}, then modify that position.", moduleId, setNames[i], colorNames[using1], colorNames[using2], colorNames[using3], k, k != 1 ? "s" : "");
            var row = colorTable.Skip(7 * using1).Take(7).ToArray();
            var newColor = Array.IndexOf(row, using3) + (7 * using1);
            for (int j = 0; j < k; j++)
                newColor = (newColor + 1) % 49;
            var movement = Array.IndexOf(row, using2);
            switch (movement)
            {
                case 0:
                    Debug.LogFormat("[Basegate #{0}] Movement: Move left {1} times.", moduleId, bomb.GetSerialNumberNumbers().Last());
                    for (int j = 0; j < bomb.GetSerialNumberNumbers().Last(); j++)
                    {
                        int x = newColor % 7, y = newColor / 7;
                        x += 6;
                        newColor = (x % 7) + 7 * (y % 7);
                    }
                    break;
                case 1:
                    Debug.LogFormat("[Basegate #{0}] Movement: Move right {1} times.", moduleId, bomb.GetSerialNumberNumbers().Last());
                    for (int j = 0; j < bomb.GetSerialNumberNumbers().Last(); j++)
                    {
                        int x = newColor % 7, y = newColor / 7;
                        x++;
                        newColor = (x % 7) + 7 * (y % 7);
                    }
                    break;
                case 2:
                    Debug.LogFormat("[Basegate #{0}] Movement: Move up {1} times.", moduleId, bomb.GetSerialNumberNumbers().First());
                    for (int j = 0; j < bomb.GetSerialNumberNumbers().First(); j++)
                    {
                        int x = newColor % 7, y = newColor / 7;
                        y += 6;
                        newColor = (x % 7) + 7 * (y % 7);
                    }
                    break;
                case 3:
                    Debug.LogFormat("[Basegate #{0}] Movement: Move down{1} times.", moduleId, bomb.GetSerialNumberNumbers().First());
                    for (int j = 0; j < bomb.GetSerialNumberNumbers().First(); j++)
                    {
                        int x = newColor % 7, y = newColor / 7;
                        y++;
                        newColor = (x % 7) + 7 * (y % 7);
                    }
                    break;
                case 4:
                    Debug.LogFormat("[Basegate #{0}] Movement: Rotate 90 degrees clockwise.", moduleId);
                    newColor = Rotate(newColor, "CW");
                    break;
                case 5:
                    Debug.LogFormat("[Basegate #{0}] Movement: Rotate 90 degrees counterclockwise.", moduleId);
                    newColor = Rotate(newColor, "CCW");
                    break;
                case 6:
                    Debug.LogFormat("[Basegate #{0}] Movement: Rotate 180 degrees.", moduleId);
                    newColor = Rotate(newColor, "180");
                    break;
            }
            Debug.LogFormat("[Basegate #{0}] {2} color: {1}.", moduleId, colorNames[colorTable[newColor]], i == 2 ? "Final" : "New");
            if (i == 0 || i == 1)
                colorsObtained.Add(newColor);
            else
                calculatedColor = colorTable[newColor];
        }

        var rawNumber = bomb.GetSerialNumberNumbers().Join("");
        Debug.LogFormat("[Basegate #{0}] The concatenated serial number digits are {1}.", moduleId, rawNumber);
        var rawNumberArray = rawNumber.ToCharArray();
        switch (calculatedColor)
        {
            case 0:
                Array.Reverse(rawNumberArray);
                rawNumber = new string(rawNumberArray);
                break;
            case 1:
                var i = rawNumber.Length - 1;
                rawNumber = rawNumber.Substring(rawNumber.Length - i) + rawNumber.Substring(0, rawNumber.Length - i);
                break;
            case 2:
                rawNumber = (int.Parse(rawNumber) * 3).ToString();
                break;
            case 3:
                rawNumber = (int.Parse(rawNumber) / 3).ToString();
                break;
            case 4:
                Array.Reverse(rawNumberArray);
                rawNumber = new string(rawNumberArray);
                break;
            case 5:
                rawNumber = rawNumber.Substring(rawNumber.Length - 1) + rawNumber.Substring(0, rawNumber.Length - 1);
                break;
            case 6:
                rawNumber = (((int.Parse(rawNumber) - 1) % 9) + 1).ToString();
                break;
        }
        calculatedNumber = int.Parse(rawNumber);
        Debug.LogFormat("[Basegate #{0}] The calculated number after the modification is {1}.", moduleId, calculatedNumber);

        var binaryNumber = Convert.ToString(calculatedNumber % 16, 2).PadLeft(4, '0');
        Debug.LogFormat("[Basegate #{0}] The number to submit in binary is {1}.", moduleId, binaryNumber);
        var binarySignificantBitArrangements = "MlLm;LMml;mlLM;LlmM;LmMl;mMLl;MlmL".Split(';').Select(str => str.Select(c => "MmlL".IndexOf(c)).ToArray()).ToArray();
        var trueIsOne = bomb.GetSerialNumberLetters().Count(x => "AEIOU".Contains(x)) % 2 == 0 ? new bool[] { false, false, true, true } : new bool[] { true, true, false, false };
        for (int i = 0; i < 4; i++)
        {
            var ix = binarySignificantBitArrangements[calculatedColor][i];
            var b = trueIsOne[i];
            correctSwitchStates[i] = binaryNumber[ix] == '0' ? (b ? false : true) : (b ? true : false);
        }
        Debug.LogFormat("[Basegate #{0}] The indices the switches have into the binary number (clockwise order) are {1}. The serial number contains an {2} amount of vowels.", moduleId, binarySignificantBitArrangements[calculatedColor].Select(x => "MmlL"[x]).Join(""), bomb.GetSerialNumberLetters().Count(x => "AEIOU".Contains(x)) % 2 == 0 ? "even" : "odd");
        Debug.LogFormat("[Basegate #{0}] Correct switch states (clockwise): {1}.", moduleId, correctSwitchStates.Select((b, i) => i % 2 == 0 ? (b ? "right" : "left") : (b ? "up" : "down")).Join(", "));

        var ternaryNumber = "000;001;002;010;011;012;020;021;022;100;101;102;110;111;112;120;121;122;200;201;202;210;211;212;220;221;222".Split(';')[calculatedNumber % 27];
        Debug.LogFormat("[Basegate #{0}] The number to submit in ternary is {1}.", moduleId, ternaryNumber);
        var letterGroups = new[] { "ABCDEFGHI", "JKLMNOPQR", "STUVWXYZ" };
        var ternarySignificantDigitArrangements = "ML-;L-M;-ML;L-M;-ML;ML-;-ML;ML-;L-M".Split(';').Select(str => str.Select(c => "M-L".IndexOf(c)).ToArray()).ToArray();
        var ternaryRowValues = "012;201;120;201;120;012;120;012;201".Split(';').Select(str => str.Select(c => int.Parse(c.ToString())).ToArray()).ToArray();
        var ternaryRow = Array.IndexOf(letterGroups, letterGroups.First(x => x.Contains(bomb.GetSerialNumber()[3])));
        var ternaryColumn = Array.IndexOf(letterGroups, letterGroups.First(x => x.Contains(bomb.GetSerialNumber()[4])));
        var usedSDA = ternarySignificantDigitArrangements[ternaryRow * 3 + ternaryColumn];
        var usedRV = ternaryRowValues[ternaryRow * 3 + ternaryColumn];
        Debug.LogFormat("[Basegate #{0}] Using row {1} and column {2}. The significant digit indices of each column from left to right are {3}, and the values for each row top to bottom are {4}.", moduleId, ternaryRow + 1, ternaryColumn + 1, usedSDA.Select(x => "M-L"[x]).Join(""), usedRV.Join(""));
        for (int i = 0; i < 3; i++)
        {
            var ix = usedSDA[i];
            var digit = ternaryNumber[ix];
            var rowIx = Array.IndexOf(usedRV, int.Parse(digit.ToString()));
            correctHatchPositions[rowIx * 3 + i] = true;
        }
        hatchIndices = correctHatchPositions.Select((b, i) => new { index = i, value = b }).Where(o => o.value).Select(obj => obj.index).ToArray();
        Debug.LogFormat("[Basegate #{0}] The correct hatch positions are {1}.", moduleId, hatchIndices.Select(x => hatchDirections[x]).Join(", "));

        var quaternaryNumber = "000;001;002;003;010;011;012;013;020;021;022;023;030;031;032;033;100;101;102;103;110;111;112;113;120;121;122;123;130;131;132;133;200;201;202;203;210;211;212;213;220;221;222;223;230;231;232;233;300;301;302;303;310;311;312;313;320;321;322;323;330;331;332;333".Split(';')[calculatedNumber % 64];
        Debug.LogFormat("[Basegate #{0}] The number to sbumit in quaternary is {1}.", moduleId, quaternaryNumber);
        var isReadingOrder = bomb.GetBatteryCount() != bomb.GetIndicators().Count() && bomb.GetIndicators().Count() != bomb.GetPortCount() && bomb.GetPortCount() != bomb.GetBatteryCount();
        Debug.LogFormat("[Basegate #{0}] The dials go in {1}reading order.", moduleId, isReadingOrder ? "" : "reverse ");
        var quaternaryDirectionValues = new int[4];
        var quaternaryCalculatedDirection = (bomb.GetModuleNames().Count() * bomb.GetSerialNumberLetters().Count() % 12) + 1;
        var closestNumbers = "11,12,1;2,3,4;5,6,7;8,9,10".Split(';').Select(str => str.Split(',').Select(ch => int.Parse(ch.ToString())).ToArray()).ToArray();
        var initialQuaternaryDirection = Array.IndexOf(closestNumbers, closestNumbers.First(arr => arr.Contains(quaternaryCalculatedDirection)));
        Debug.LogFormat("[Basegate #{0}] The calculated number for this step is {1}, which is closest to {2} on a clock.", moduleId, quaternaryCalculatedDirection, new[] { "12", "3", "6", "9" }[initialQuaternaryDirection]);
        for (int i = 0; i < 4; i++)
            quaternaryDirectionValues[(initialQuaternaryDirection + i) % 4] = 3 - i;
        for (int i = 0; i < 3; i++)
        {
            var dir = Array.IndexOf(quaternaryDirectionValues, int.Parse(quaternaryNumber[i].ToString()));
            correctDialPositions[isReadingOrder ? i : 2 - i] = dir;
            Debug.LogFormat("[Basegate #{0}] Dial {1} should be set to {2}.", moduleId, i + 1, new[] { "up", "right", "down", "left" }[dir]);
        }
    }

    private void FlipSwitch(KMSelectable bigSwitch)
    {
        var ix = Array.IndexOf(switches, bigSwitch);
        switchStates[ix] = !switchStates[ix];
        audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonRelease, bigSwitch.transform);
        if (switchMovements[ix] != null)
        {
            StopCoroutine(switchMovements[ix]);
            switchMovements[ix] = null;
        }
        switchMovements[ix] = StartCoroutine(MoveSwitch(bigSwitch.transform, bigSwitch.transform.localEulerAngles.x, switchStates[ix] ? 55f : -55f, ix % 2 == 0 ? 90f : 0f));
    }

    private void PressHatch(KMSelectable hatch)
    {
        var ix = Array.IndexOf(hatchCovers, hatch);
        if (moduleSolved || hatchesOpened[ix])
            return;
        if (correctHatchPositions[ix])
            Debug.LogFormat("[Basegate #{0}] Opened the {1} hatch, which is correct.", moduleId, hatchDirections[ix]);
        else
        {
            Debug.LogFormat("[Basegate #{0}] Tried to open the {1} hatch, which is incorrect. Strike!", moduleId, hatchDirections[ix]);
            module.HandleStrike();
            return;
        }
        dials[ix].gameObject.SetActive(true);
        hatchHighlights[ix].SetActive(false);
        hatchesOpened[ix] = true;
        StartCoroutine(OpenHatch(hatch.transform, ix));
        StartCoroutine(ElevateDial(dials[ix].transform));
    }

    private void PressDial(KMSelectable dial)
    {
        var ix = Array.IndexOf(dials, dial);
        var direction = (dialPositions[Array.IndexOf(dials, dial)] + 5) % 4;
        if (dialMovements[ix] != null)
        {
            StopCoroutine(dialMovements[ix]);
            dialMovements[ix] = null;
        }
        if (dialPositions[ix] != direction)
        {
            dialPositions[ix] = direction;
            dialMovements[ix] = StartCoroutine(MoveDial(dialPositions[ix], dialTransforms[ix]));
        }
    }

    private void PressSubmitButton()
    {
        submitButton.AddInteractionPunch(.5f);
        audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, submitButton.transform);
        if (moduleSolved)
            return;
        var wrongReasons = new List<int>();
        if (!hatchesOpened.SequenceEqual(correctHatchPositions))
            wrongReasons.Add(0);
        if (!switchStates.SequenceEqual(correctSwitchStates))
            wrongReasons.Add(1);
        var relevantPositions = Enumerable.Range(0, 9).Where(x => correctHatchPositions[x]).Select(x => dialPositions[x]).ToList();
        if (!relevantPositions.SequenceEqual(correctDialPositions))
            wrongReasons.Add(2);
        if (wrongReasons.Count() != 0)
        {
            var reasonNames = new[] { "not all of the correct hatches have been open", "the switches weren't set correctly", "the dials weren't set correctly" };
            Debug.LogFormat("[Basegate #{0}] Submitted a configuration which was incorrect because {1}. Strike!", moduleId, wrongReasons.Select(x => reasonNames[x]).Join(", "));
            module.HandleStrike();
        }
        else
        {
            Debug.LogFormat("[Basegate #{0}] Submitted the correct configuration. Module solved!", moduleId);
            module.HandlePass();
            moduleSolved = true;
            audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.CorrectChime, transform);
        }
    }

    private IEnumerator MoveSwitch(Transform bigSwitch, float start, float end, float y)
    {
        var elapsed = 0f;
        var duration = .3f;
        while (elapsed < duration)
        {
            //bigSwitch.localEulerAngles = new Vector3(Easing.OutSine(elapsed, start, end, duration), y, 0f);
            bigSwitch.localRotation = Quaternion.Slerp(Quaternion.Euler(start, y, 0f), Quaternion.Euler(end, y, 0f), elapsed / duration);
            yield return null;
            elapsed += Time.deltaTime;
        }
        bigSwitch.localEulerAngles = new Vector3(end, y, 0f);
    }

    private IEnumerator OpenHatch(Transform hatch, int ix)
    {
        var elapsed = 0f;
        var duration = 1f;
        var startPos = hatch.localPosition.z;
        var endPos = startPos + .00971f;
        var xPos = hatch.localPosition.x;
        audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.WireSequenceMechanism, hatch);
        while (elapsed < duration)
        {
            hatch.localScale = new Vector3(.025f, .00095f, Mathf.Lerp(.025f, .0001f, elapsed / duration));
            hatch.localPosition = new Vector3(xPos, .0131f, Mathf.Lerp(startPos, endPos, elapsed / duration));
            yield return null;
            elapsed += Time.deltaTime;
        }
        hatch.gameObject.SetActive(false);
    }

    private IEnumerator ElevateDial(Transform dial)
    {
        var elapsed = 0f;
        var duration = 1.5f;
        var x = dial.localPosition.x;
        var z = dial.localPosition.z;
        var startPos = dial.localPosition.y;
        var endPos = 1.14f;
        while (elapsed < duration)
        {
            dial.localPosition = new Vector3(x, Easing.OutSine(elapsed, startPos, endPos, duration), z);
            yield return null;
            elapsed += Time.deltaTime;
        }
        dial.localPosition = new Vector3(x, endPos, z);
    }

    private IEnumerator MoveDial(int direction, Transform dial)
    {
        var elapsed = 0f;
        var duration = .25f;
        var startRotation = dial.localRotation;
        var endRotation = Quaternion.Euler(0f, 90f * direction, 0f);
        audio.PlaySoundAtTransform("dial click", dial);
        while (elapsed < duration)
        {
            dial.localRotation = Quaternion.Slerp(startRotation, endRotation, elapsed / duration);
            yield return null;
            elapsed += Time.deltaTime;
        }
        dial.localRotation = endRotation;
    }

    private IEnumerator DisableStuff()
    {
        yield return null;
        for (int i = 0; i < 9; i++)
            dials[i].gameObject.SetActive(false);
    }

    private static int Rotate(int pos, string instruction)
    {
        int x = pos % 7, y = pos / 7;
        int x2 = x, y2 = y;
        switch (instruction)
        {
            case "CW": y = x2; x = 6 - y2; break;
            case "CCW": y = 6 - x2; x = y2; break;
            case "180": x = 6 - x; y = 6 - y; break;
            default: break;
        }
        return (x % 7) + 7 * (y % 7);
    }


    // Twitch Plays
#pragma warning disable 414
    private readonly string TwitchHelpMessage = "!{0} flip <up/right/down/left> [Flips the switch in that direction, initial letters can also be used] | !{0} open <top-left/middle-middle/bottom-right> [Opens the hatch in that position, abbreviations can also be used, e.g. TL] | !{0} set top-left up [If the top-left hatch is open, rotates the dial under it to the up position, initial letters and cardinal directions can also be used] | !{0} submit [Presses the submit button]";
#pragma warning restore 414

    private IEnumerator ProcessTwitchCommand(string input)
    {
        input = input.Trim().ToLowerInvariant();
        var inputArray = input.Split(' ').ToArray();
        var cardinalDirections = new[] { "up", "down", "left", "right", "u", "d", "l", "r", "top", "bottom", "t", "b", "north", "south", "east", "west", "n", "s", "e", "w" };
        var gridDirections = new[] { "top-left", "top-middle", "top-center", "top-centre", "top-right", "middle-left", "center-left", "centre-left", "middle-middle", "middle", "center", "centre", "center-center", "centre-centre", "dead-center", "middle-right", "center-right", "centre-right", "bottom-left", "bottom-middle", "bottom-center", "bottom-centre", "bottom-right", "tl", "tm", "tc", "tr", "ml", "cl", "mm", "cc", "cm", "mc", "dc", "mr", "cr", "bl", "bm", "bc", "br" };
        if (input == "submit")
        {
            yield return null;
            submitButton.OnInteract();
        }
        else if (inputArray[0] == "flip" && inputArray.Length == 2 && cardinalDirections.Contains(inputArray[1]))
        {
            yield return null;
            switches[TPGetCardinalDirection(inputArray[1])].OnInteract();
        }
        else if (inputArray[0] == "open" && inputArray.Length == 2 && gridDirections.Contains(inputArray[1]))
        {
            yield return null;
            var ix = TPGetGridDirection(inputArray[1]);
            if (hatchesOpened[ix])
            {
                yield return "sendtochaterror That hatch is already open, dummy.";
                yield break;
            }
            hatchCovers[ix].OnInteract();
        }
        else if (inputArray[0] == "set" && inputArray.Length == 3 && gridDirections.Contains(inputArray[1]) && cardinalDirections.Contains(inputArray[2]))
        {
            yield return null;
            var ix1 = TPGetGridDirection(inputArray[1]);
            var ix2 = TPGetCardinalDirection(inputArray[2]);
            if (!hatchesOpened[ix1])
            {
                yield return "sendtochaterror That hatch isn't open yet, dummy.";
                yield break;
            }
            if (ix2 == dialPositions[ix1])
            {
                yield return "sendtochaterror That dial is already set to that, dummy.";
                yield break;
            }
            while (dialPositions[ix1] != ix2)
            {
                yield return new WaitForSeconds(.1f);
                dials[ix1].OnInteract();
            }
        }
        else
            yield break;
    }

    private IEnumerator TwitchHandleForcedSolve()
    {
        for (int i = 0; i < 4; i++)
        {
            if (correctSwitchStates[i] != switchSockets[i])
            {
                yield return new WaitForSeconds(.1f);
                switches[i].OnInteract();
            }
        }
        for (int i = 0; i < 9; i++)
        {
            if (!hatchesOpened[i] && correctHatchPositions[i])
            {
                yield return new WaitForSeconds(.1f);
                hatchCovers[i].OnInteract();
            }
        }
        var ix = 0;
        for (int i = 0; i < 9; i++)
        {
            if (hatchesOpened[i])
            {
                while (dialPositions[i] != correctDialPositions[ix])
                {
                    yield return new WaitForSeconds(.1f);
                    dials[i].OnInteract();
                }
                ix++;
            }
        }
        yield return new WaitForSeconds(.1f);
        submitButton.OnInteract();
    }

    private static int TPGetCardinalDirection(string str)
    {
        switch (str)
        {
            case "up":
            case "u":
            case "top":
            case "t":
            case "north":
            case "n":
                return 0;
            case "right":
            case "r":
            case "east":
            case "e":
                return 1;
            case "down":
            case "d":
            case "bottom":
            case "b":
            case "south":
            case "s":
                return 2;
            case "left":
            case "l":
            case "west":
            case "w":
                return 3;
            default:
                return -1;
        }
    }

    private static int TPGetGridDirection(string str)
    {
        switch (str)
        {
            case "top-left":
            case "tl":
                return 0;
            case "top-middle":
            case "top-center":
            case "top-centre":
            case "tm":
            case "tc":
                return 1;
            case "top-right":
            case "tr":
                return 2;
            case "middle-left":
            case "center-left":
            case "centre-left":
            case "ml":
            case "cl":
                return 3;
            case "middle":
            case "center":
            case "centre":
            case "middle-middle":
            case "center-center":
            case "centre-centre:":
            case "dead-center":
            case "mm":
            case "cc":
            case "mc":
            case "cm":
            case "dc":
                return 4;
            case "middle-right":
            case "center-right":
            case "centre-right":
            case "mr":
            case "cr":
                return 5;
            case "bottom-left":
            case "bl":
                return 6;
            case "bottom-middle":
            case "bottom-center":
            case "bottom-centre":
            case "bm":
            case "bc":
                return 7;
            case "bottom-right":
            case "br":
                return 8;
            default:
                return -1;
        }
    }
}
