# Build Plan: VR Marksmanship Trainer with EEG Attention Feedback

## 1. What is being built

A Meta Quest VR range-marksmanship trainer. The user stands or lies at a virtual firing range, looks through a rifle scope at distant paper or steel targets, and fires with the controller trigger. A wearable EEG headset (Emotiv EPOC X) streams the user’s live attention level into the app, and that level controls scope steadiness: low attention makes the reticle sway and the scope edges blur, high attention steadies and clears it. There are no people or enemies, only targets.

Every shot is logged with the attention value at that moment and the shot’s accuracy. This lets the project show whether attention and accuracy move together, and whether attention feedback tightens shot groupings across sessions.

**Design rule that keeps the build safe:** all attention input flows through one value, `AttentionLevel` (0 to 1). The whole app can be built, tested, and demoed on a simulated value, and live EEG is swapped in at the end without touching anything else.

---

## 2. Requirements

**Hardware**

| Item | Notes |
| --- | --- |
| Windows 10/11 PC with a capable GPU | Meta Quest Link is not available on macOS. Check the GPU against the [Quest Link requirements](https://www.meta.com/en-gb/help/quest/articles/headsets-and-accessories/oculus-link/meta-quest-link-compatibility/). |
| Meta Quest headset + Link cable | The base project is tested on Quest 2 over Link. Test other Quest models early. |
| Emotiv EPOC X + USB receiver (or Bluetooth LE) | Saline-felt sensors; keep saline solution on hand. Link cable and Emotiv receiver each need a USB port. |

**Software**

| Item | Notes |
| --- | --- |
| Unity Hub + **Unity 2021.3.1f1** | Exact version the base project was built on. Newer versions may work but are untested. |
| Visual Studio (installed via Unity Hub) | For C# scripting. |
| Git | Version control. |
| Meta Quest Link (PC app) | Streams the Unity scene to the headset. |
| EMOTIV Launcher | Must be running and logged in whenever the app runs. |
| EmotivID account + Cortex App ID | See Phase 0. |
| Python (pandas, matplotlib) or Excel | For analysing logged data. |

---

## 3. Settle these before writing code

1. **Emotiv licence tier decides signal quality.** On EPOC X, the free Basic API includes performance metrics at only 0.1 Hz (one update per 10 seconds), band-power (frequency band) data, and motion data. Raw EEG and 2 Hz performance metrics need a paid Premium developer licence. A 10-second update rate is too slow to drive live scope sway, so either obtain the Premium licence (apply now, see Phase 0) or use the band-power fallback in Phase 5. Check whether the institution already holds an Emotiv licence.
2. **Physical fit.** The EPOC X headset must sit under the Quest strap with good sensor contact. Test this in Phase 0, before anything else depends on it.
3. **Plugin compatibility is unverified.** Emotiv’s Unity plugin does not state supported Unity versions. Confirm it compiles on 2021.3.1 early (Phase 5). The fallback is talking to Cortex directly over WebSocket.
4. **EEG attention metrics are noisy.** Blinks, jaw clenching, and head movement distort them. Plan for this in testing and state it as a limitation in the report.

---

## 4. Order of work

Time estimates are rough and assume working familiarity with Unity and C#.

### Phase 0: Accounts, licences, environment (about 1 day, plus licence wait)

1. Create an EmotivID, install the EMOTIV Launcher, log in, pair the EPOC X, and check sensor contact quality.
2. Register a Cortex App on the SDK application form to get a **Client ID and Client Secret**. Store the secret outside the repository. For Premium streams (2 Hz metrics, raw EEG), contact Emotiv support *before* registering so the licence is attached to the account.
3. In the Launcher, create a **Virtual Brainwear** (virtual headset) so development can continue without the physical headset.
4. Install Unity Hub and Unity 2021.3.1f1 from the Unity archive. Add a free personal licence.
5. Install Meta Quest Link, set up the Quest, and confirm it works over Link.
6. **Fit test:** wear the EPOC X (felts hydrated) under the Quest with the strap fitted, and confirm contact quality stays good in the Launcher.

**Done when:** Launcher shows good contact with the Quest on, the Quest runs over Link, Client ID and Secret are in hand, and the licence status (Basic or Premium) is known.

### Phase 1: Base project running on Quest (about 0.5 day)

1. Clone [NEUROSPEC-AG/VR-EEG-Sample-Experiment](https://github.com/NEUROSPEC-AG/VR-EEG-Sample-Experiment) and add it in Unity Hub with editor version 2021.3.1f1. Unity needs internet on first open to restore packages.
2. Start Quest Link, press Play in Unity, and walk through the Welcome, Trial, and End scenes.
3. Strip what is not needed: the jewelry-shop scene content (a third-party asset) and the `NeurospecTriggerBox-Unity-master` folder with any trigger scripts and UI that reference it (that is for a hardware trigger box this project does not use).
4. Keep the XR rig, the XR Interaction Toolkit setup, the OpenXR settings, the three-scene flow, and the participant-ID entry (reused for logging).
5. Commit to a fresh repository. Keep the MIT `LICENSE` and credit NEUROSPEC AG.

**Done when:** a minimal scene runs on the Quest with working head tracking and controller input.

### Phase 2: Attention abstraction and simulator (about 0.5 day)

Create one interface that everything else depends on:

```csharp
public interface IAttentionSource
{
    float Level { get; }          // 0..1, higher = more focused
    bool  IsSignalValid { get; }  // false if headset lost or contact is poor
}
```

- `SimulatedAttentionSource`: driven by keyboard or an inspector slider, plus an optional slow random walk.
- `AttentionHub`: holds the active source, applies exponential smoothing, and exposes `Level` and `IsSignalValid` to the rest of the app.
- A small debug HUD showing the live value.

**Done when:** changing the simulated value changes `AttentionHub.Level`, and swapping the source needs no changes anywhere else.

### Phase 3: Range scene and scope mechanic (about 2 to 3 days)

1. **Range:** flat terrain, a firing line, and 5 to 8 targets at varied distances. Use free low-poly range and rifle assets from the Unity Asset Store (check each licence).
2. **Scope view:** a scope camera with a reticle overlay. Pick whichever approach reads most clearly in VR (render texture on a scope lens, or a framed scope view when aiming).
3. **Sway and blur, driven by `AttentionHub.Level`:**
   - Sway: Perlin noise on the scope camera’s yaw and pitch, amplitude = `Lerp(maxSway, minSway, Level)`.
   - Blur or vignette strength = `1 - Level`.
   - Start with `maxSway ≈ 1.5°` and `minSway ≈ 0.1°`, exposed as tunable values.
   - Apply sway to the scope view only, not the whole world, to limit motion discomfort.
4. **Firing:** trigger press raycasts along the scope camera’s forward direction. Compute the offset from the target centre, convert to a ring score, and play a sound and hit marker. A straight raycast is enough; ballistics are not needed.
5. **Feedback toggle (ON/OFF):** when OFF, sway uses a fixed moderate value that ignores attention, while attention is still recorded. This is the control condition for the study.

**Done when:** a full shooting session is playable on simulated attention, with shots scored.

### Phase 4: Session flow and logging (about 1 day)

1. Welcome scene: enter participant ID, session number, and condition (feedback ON/OFF).
2. Trial scene: fixed shots per block (for example 20 to 30) with a short rest between blocks.
3. Write two CSV files per session to the persistent data path:
   - **Shots:** timestamp, participant, session, condition, shot number, attention level at shot, sway amplitude, offset from centre, score, reaction time.
   - **Attention trace:** timestamp, raw value, smoothed value, signal-valid flag.
4. Optional summary screen in the End scene (mean score, mean offset).

**Done when:** a session produces both CSVs and they open cleanly in Excel or pandas.

### Phase 5: Live EEG integration (about 1 to 2 days)

1. Import the [Emotiv Unity plugin](https://github.com/Emotiv/unity-plugin) (the `Src` folder) into the project. Resolve any compile errors or missing packages.
2. Use the **Cortex Service (desktop) option**: install and log in to the EMOTIV Launcher before running, and make sure the `USE_EMBEDDED_LIB` scripting symbol is *not* defined.
3. Through `EmotivUnityItf`: call `Init` with Client ID, Client Secret, and app name, then `Start`, then `QueryHeadsets`, `CreateSessionWithHeadset`, and `SubscribeData` with the chosen stream. Read the `cols` array in the subscribe response to learn the column order.
4. **Choose the signal:**
   - **If Premium (2 Hz metrics) is available:** subscribe to `met`. Its columns include focus (`foc`) and engagement (`eng`). Start with focus as `AttentionLevel`, log engagement alongside, and decide which tracks accuracy better after pilots.
   - **If Basic only:** subscribe to `pow` (band power) and compute an engagement index on frontal channels (for example beta divided by alpha plus theta), then normalise it per user.
5. **Per-session baseline:** record 60 to 90 seconds of resting data at the start of each session and normalise `Level` to the wearer’s own range, since raw values vary a lot between people and sittings.
6. **Signal gating:** subscribe to the contact-quality stream (`eq`). If contact is poor or the headset drops, set `IsSignalValid` to false, pause the feedback effect, and show a warning.
7. Implement `EmotivAttentionSource : IAttentionSource` and select it in `AttentionHub`.
8. Keep the Client Secret in a git-ignored config file, never in the repo.

**Done when:** with the Launcher running, the live value visibly changes with the wearer’s state (for example eyes closed versus mental arithmetic), and switching between simulated and live sources needs no other code changes.

### Phase 6: Integrated testing on Quest + EPOC X (about 1 day)

1. Run the full loop and check update rate and perceived latency.
2. Check sensor contact holds under the Quest strap over a 15-minute session, and check comfort and any motion discomfort from the sway.
3. Tune smoothing, sway range, and blur strength with two or three testers.
4. Brief testers to stay still and avoid jaw clenching; note artefacts in a test log.

**Done when:** three consecutive full sessions run with no crashes or signal dropouts.

### Phase 7: Pilot study (about 2 to 3 days)

- **Design:** within-subject, feedback ON versus OFF, with condition order counterbalanced. A small pilot (for example 5 to 10 participants, 2 to 3 sessions each) is realistic; adjust to availability.
- **Procedure per session:** consent, fit and hydrate headset, contact check, resting baseline, 5 practice shots, block A, rest, block B.
- **Measures:** mean offset from centre, ring score, shot-to-shot variability, mean attention, within-person correlation of attention with offset, and change across sessions.
- **Analysis:** pandas and matplotlib or Excel. Two clear plots are enough: accuracy by condition, and attention versus offset scatter.
- **Reporting:** with a small sample, report results descriptively and do not claim statistical significance.
- **Ethics:** use informed consent and anonymised participant IDs, and confirm the institution’s requirements for EEG recording on human participants before recruiting.

**Done when:** all planned sessions are logged and the analysis script reproduces the summary plots from the CSVs.

### Phase 8: Packaging and handover (about 1 day)

1. Final build: run from the Unity Editor over Link, or make a Windows standalone build with OpenXR enabled.
2. Record a demo video of the PC mirror window showing both the scope view and live attention value.
3. Write a README covering setup, licences, how to run, and how to switch between simulated and live attention.
4. Write a short report: problem, system, study protocol, results, limitations.
5. Tag a release in git.

**Done when:** someone else can clone the repository, follow the README, and run the simulated version without help.

---

## 5. Fallbacks

| If this fails | Do this |
| --- | --- |
| Premium Emotiv licence not available | Use the `pow` band-power stream and compute an engagement index (Phase 5). |
| Emotiv Unity plugin does not compile on 2021.3 | Connect to Cortex directly over JSON/WebSocket using the [Cortex examples](https://github.com/Emotiv/cortex-v2-example) as reference. |
| EPOC X not available or fit is unworkable | Ship with the simulated source, clearly labelled as simulated, and keep the live-EEG integration as documented future work. |
| Quest Link unstable on the PC | Check the GPU against Meta’s requirements, use a USB 3 port and quality cable, and see Meta’s Link setup guide. |

---

## 6. Final deliverables checklist

- [ ] Working Unity project in a git repository (MIT licence and NEUROSPEC credit retained)
- [ ] Simulated and live attention sources, switchable
- [ ] Logged shot and attention CSVs from pilot sessions
- [ ] Analysis script and summary plots
- [ ] Demo video
- [ ] README with setup instructions
- [ ] Short report with limitations stated

---

## 7. Links and resources

**Base project and plugin**

- Base Unity + Quest project: [https://github.com/NEUROSPEC-AG/VR-EEG-Sample-Experiment](https://github.com/NEUROSPEC-AG/VR-EEG-Sample-Experiment)
- Emotiv Unity plugin (Cortex V3): [https://github.com/Emotiv/unity-plugin](https://github.com/Emotiv/unity-plugin)
- Plugin release notes: [https://github.com/Emotiv/unity-plugin/blob/master/Documentation/ReleaseNotes.md](https://github.com/Emotiv/unity-plugin/blob/master/Documentation/ReleaseNotes.md)
- Emotiv Cortex Unity example: [https://github.com/Emotiv/cortex-example/tree/master/unity](https://github.com/Emotiv/cortex-example/tree/master/unity)
- Emotiv Cortex examples (other languages, WebSocket reference): [https://github.com/Emotiv/cortex-v2-example](https://github.com/Emotiv/cortex-v2-example)

**Emotiv setup and API**

- EMOTIV Launcher: [https://www.emotiv.com/products/emotiv-launcher](https://www.emotiv.com/products/emotiv-launcher)
- Cortex SDK application / app registration: [https://www.emotiv.com/cortex-sdk-application-form](https://www.emotiv.com/cortex-sdk-application-form)
- How to register an App ID (access levels explained): [https://www.emotiv.com/knowledge-base/how-to-register-your-app-id-with-cortex-sdk-for-development](https://www.emotiv.com/knowledge-base/how-to-register-your-app-id-with-cortex-sdk-for-development)
- Cortex API documentation: [https://emotiv.gitbook.io/cortex-api](https://emotiv.gitbook.io/cortex-api)
- Data subscription (`subscribe`, stream list): [https://emotiv.gitbook.io/cortex-api/data-subscription/subscribe](https://emotiv.gitbook.io/cortex-api/data-subscription/subscribe)
- Virtual Brainwear (virtual headset for development): [https://emotiv.gitbook.io/emotiv-launcher/devices-setting-up-virtual-brainwear-r](https://emotiv.gitbook.io/emotiv-launcher/devices-setting-up-virtual-brainwear-r)
- Cortex system requirements: [https://emotiv.gitbook.io/system-requirements/cortex-api](https://emotiv.gitbook.io/system-requirements/cortex-api)
- Emotiv developer program: [https://www.emotiv.com/knowledge-base/getting-started-with-the-emotiv-developer-program](https://www.emotiv.com/knowledge-base/getting-started-with-the-emotiv-developer-program)
- Emotiv support (Premium licence requests): [https://www.emotiv.com/contact](https://www.emotiv.com/contact)
- EPOC X product page: [https://www.emotiv.com/epoc-x](https://www.emotiv.com/epoc-x)
- Emotiv community forum: [https://forum.emotiv.com/](https://forum.emotiv.com/)

**Unity and Meta Quest**

- Unity Hub download: [https://unity.com/download](https://unity.com/download)
- Unity download archive (get 2021.3.1f1): [https://unity.com/releases/editor/archive](https://unity.com/releases/editor/archive)
- Meta Quest Link requirements: [https://www.meta.com/en-gb/help/quest/articles/headsets-and-accessories/oculus-link/meta-quest-link-compatibility/](https://www.meta.com/en-gb/help/quest/articles/headsets-and-accessories/oculus-link/meta-quest-link-compatibility/)
- Meta Quest Link setup: [https://www.meta.com/help/quest/articles/headsets-and-accessories/oculus-link/set-up-link/](https://www.meta.com/help/quest/articles/headsets-and-accessories/oculus-link/set-up-link/)
- Unity with Quest Link (Meta developer docs): [https://developer.oculus.com/documentation/unity/unity-link/](https://developer.oculus.com/documentation/unity/unity-link/)
- Meta Unity project setup: [https://developers.meta.com/horizon/documentation/unity/unity-project-setup/](https://developers.meta.com/horizon/documentation/unity/unity-project-setup/)
- Unity OpenXR plugin manual: [https://docs.unity3d.com/Manual/com.unity.xr.openxr.html](https://docs.unity3d.com/Manual/com.unity.xr.openxr.html)
- Unity forum: [https://forum.unity.com/](https://forum.unity.com/)
- Meta Quest developer forum: [https://communityforums.atmeta.com/](https://communityforums.atmeta.com/)

**Background reading**

- FractalBrain (EEG + Quest + Unity neurofeedback architecture, CHI 2024): [https://arxiv.org/abs/2510.26041](https://arxiv.org/abs/2510.26041)
- Review of VR-based feedback in neurofeedback applications: [https://www.ncbi.nlm.nih.gov/pmc/articles/PMC12686094/](https://www.ncbi.nlm.nih.gov/pmc/articles/PMC12686094/)
- EEG-controlled VR shooting game (protocol and report structure only; Unity 5.5 / Rift / NeuroSky, not reusable as code): [https://github.com/liyichen7887/EEG_VR_SHOOTING](https://github.com/liyichen7887/EEG_VR_SHOOTING)