# HSE Demo - VR Nurse Training (Meta Quest 3)

Blood collection and lab request training in a hospital room, built with Unity 6.3, URP and XR Interaction Toolkit (hands or controllers).

## Scenario (all steps mandatory, in order, following the GDD)
1. Hand hygiene, then gloves (hands turn into nitrile gloves)
2. Take the PDA, go to the patient, introduce yourself (the patient states name and DOB)
3. Scan the QR wristband and confirm identity on the PDA
4. Blood pressure: cuff on the arm, NIBP START on the monitor, record on the PDA
5. Blood draw: explain and get consent, tourniquet, alcohol swab, needle at 15-30 degrees, fill the ordered tubes, release tourniquet, withdraw, sharps box, gauze
6. Labelling: scan the wristband again, print labels on the PDA, stick each label on its tube
7. Documentation: blood collection record on the PDA (site, tolerance, tubes, vitals)
8. Lab request on the PDA (tests must match the physician's order); the request slip prints
9. Dispatch: tubes and slip into the specimen bag, seal, scan the bag, send by pneumatic tube; results arrive after dispatch

Instructor variants on the entrance panel: Standard, Wrong wristband (report the ID mismatch), Critical BP (escalate before recording), Consent refused (stop, document, notify - ends the scenario early). The results screen shows the GDD's nine scoring categories.
Five fictional patients (Peter Glynne, Ender Erol, Ali Erol, Margaret Hughes, Samuel Okafor), each with their own vitals, care board, physician orders, lab results and an "AI assistant" comment. Press **Next patient** at the end to play again with another patient. English by default, Turkish selectable at the entrance.

## Features
- Task system with guidance arrows and [PrismFX Highlight](https://github.com/endererolwork/PrismVFX-Highlight) (see `Assets/HSE/Scenario/README.md`, SOLID design, PlayMode tests)
- PDA app: patient details, orders, vitals charts, lab results
- Live bedside monitor, lab report panel with normal / borderline / high / low / critical flags
- Furniture and PDA modelled in Blender from scripts in `Tools/Blender`; CC0 textures from Poly Haven

## Run
Open `Assets/HSE/Scenes/PatientRoom.unity` and press Play (an outdated scene rebuilds itself). Menu **HSE > Build Patient Room Scene** rebuilds it manually.
