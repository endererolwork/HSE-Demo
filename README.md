# HSE Demo - VR Nurse Training (Meta Quest 3)

Blood collection and lab request training in a hospital room, built with Unity 6.3, URP and XR Interaction Toolkit (hands or controllers).

## Scenario (all steps mandatory, in order)
1. Hand hygiene, then gloves (hands turn into nitrile gloves)
2. Take the PDA from the procedure trolley
3. Go to the patient
4. Scan the QR wristband (trigger or the SCAN button: red laser fan)
5. Confirm the patient's identity on the PDA
6. Measure blood pressure: cuff on the arm, NIBP START on the monitor, record on the PDA
7. Take the blood sample: tourniquet, alcohol swab, needle at 15-30 degrees, fill the ordered tubes, release the tourniquet, withdraw, sharps box, gauze
8. Send the lab request that matches the physician's order

Safety rules are checked: no needle without tourniquet and cleaned skin, tourniquet off before the needle comes out, only the tubes the order needs.

Five fictional patients (Peter Glynne, Ender Erol, Ali Erol, Margaret Hughes, Samuel Okafor), each with their own vitals, care board, physician orders, lab results and an "AI assistant" comment. Press **Next patient** at the end to play again with another patient. English by default, Turkish selectable at the entrance.

## Features
- Task system with guidance arrows and [PrismFX Highlight](https://github.com/endererolwork/PrismVFX-Highlight) (see `Assets/HSE/Scenario/README.md`, SOLID design, PlayMode tests)
- PDA app: patient details, orders, vitals charts, lab results
- Live bedside monitor, lab report panel with normal / borderline / high / low / critical flags
- Furniture and PDA modelled in Blender from scripts in `Tools/Blender`; CC0 textures from Poly Haven

## Run
Open `Assets/HSE/Scenes/PatientRoom.unity` and press Play (an outdated scene rebuilds itself). Menu **HSE > Build Patient Room Scene** rebuilds it manually.
