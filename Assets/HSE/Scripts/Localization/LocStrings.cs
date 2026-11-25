using System.Collections.Generic;

namespace HSE.Localization
{
    /// <summary>All UI text, English first. Steps use "step.&lt;id&gt;.title|instr|cue".</summary>
    internal static class LocStrings
    {
        public static readonly Dictionary<string, (string en, string tr)> Table = new Dictionary<string, (string en, string tr)>
        {
            // ---------------------------------------------------------------- language menu
            ["lang.title"] = ("Choose your language", "Dilinizi seçin"),
            ["lang.subtitle"] = ("Blood Collection and Lab Request training", "Kan Alma ve Laboratuvar İstemi eğitimi"),
            ["lang.english"] = ("English", "English"),
            ["lang.turkish"] = ("Türkçe", "Türkçe"),
            ["lang.hint"] = ("Point and pinch (or pull the trigger) to choose.", "Seçmek için işaret edip parmaklarınızı birleştirin (veya tetiğe basın)."),

            // ---------------------------------------------------------------- scenario / checklist
            ["scenario.title"] = ("Blood Collection and Lab Request", "Kan Alma ve Laboratuvar İstemi"),
            ["checklist.extras"] = ("EXTRA TASKS", "EK GÖREVLER"),
            ["checklist.skipped"] = ("skipped", "atlandı"),
            ["checklist.mistake"] = ("Mistake:", "Hata:"),
            ["checklist.done"] = ("Completed", "Tamamlandı"),
            ["checklist.results"] = ("Scenario finished. Results:", "Senaryo bitti. Sonuçlar:"),
            ["checklist.score"] = ("Score", "Puan"),
            ["checklist.mistakes"] = ("Mistakes", "Hata sayısı"),
            ["checklist.time"] = ("Time", "Süre"),
            ["checklist.extrasDone"] = ("Extra tasks", "Ek görevler"),
            ["guide.done"] = ("Well done! Scenario complete.", "Tebrikler! Senaryo tamamlandı."),

            // ---------------------------------------------------------------- steps
            ["step.take_pda.title"] = ("Take the handheld", "El terminalini al"),
            ["step.take_pda.instr"] = ("Pick up the PDA from the procedure trolley.", "Tedavi arabasındaki PDA'yı al."),
            ["step.take_pda.cue"] = ("Take the PDA", "PDA'yı al"),

            ["step.approach.title"] = ("Go to the patient", "Hastanın yanına git"),
            ["step.approach.instr"] = ("Walk to the right side of the bed.", "Hasta yatağının sağ tarafına geç."),
            ["step.approach.cue"] = ("Go to the bedside", "Yatağın yanına git"),

            ["step.scan_wristband.title"] = ("Scan the wristband", "Hasta bilekliğini okut"),
            ["step.scan_wristband.instr"] = ("Pull the trigger or press SCAN on the PDA and point the laser at the QR code on the patient's right wrist.",
                "Tetiğe veya PDA'daki TARA tuşuna bas, lazeri hastanın sağ bileğindeki QR koda doğrult."),
            ["step.scan_wristband.cue"] = ("Scan the wristband", "Bilekliği okut"),

            ["step.confirm_identity.title"] = ("Confirm identity", "Kimliği doğrula"),
            ["step.confirm_identity.instr"] = ("Compare the PDA details with the patient and press \"Confirm Identity\".",
                "PDA'daki bilgileri hastayla karşılaştır ve \"Kimliği Doğrula\"ya bas."),
            ["step.confirm_identity.cue"] = ("Confirm identity on the PDA", "PDA'da kimliği doğrula"),

            ["step.hand_hygiene.title"] = ("Hand hygiene and gloves", "El hijyeni ve eldiven"),
            ["step.hand_hygiene.instr"] = ("Sanitise your hands before touching the patient, then put on gloves.",
                "Hastaya dokunmadan önce ellerini dezenfekte et, sonra eldiven giy."),
            ["step.hand_hygiene.cue"] = ("Clean your hands", "Ellerini temizle"),
            ["step.hand_rub.title"] = ("Sanitise hands", "Ellerini dezenfekte et"),
            ["step.hand_rub.instr"] = ("Use the hand rub dispenser.", "Antiseptik dispenserine dokun."),
            ["step.hand_rub.cue"] = ("Use the hand rub", "Antiseptiği kullan"),
            ["step.gloves.title"] = ("Put on gloves (M)", "Eldiven giy (M)"),
            ["step.gloves.instr"] = ("Take gloves from the size M box.", "M beden eldiven kutusundan eldiven al."),
            ["step.gloves.cue"] = ("Take gloves (M)", "Eldiven al (M)"),

            ["step.lab_order.title"] = ("Send the lab request", "Tahlil istemi yap"),
            ["step.lab_order.instr"] = ("Open \"Lab Request\" on the PDA, select the tests the physician ordered and send.",
                "PDA'da \"Tahlil İstemi\"ni aç, hekim istemindeki testleri seç ve gönder."),
            ["step.lab_order.cue"] = ("Send the lab request", "Tahlil istemi gönder"),

            ["step.read_care_board.title"] = ("Check the care board", "Bakım panosunu kontrol et"),
            ["step.read_care_board.instr"] = ("Look at the care board in the room.", "Odadaki bilgi panosuna bak."),
            ["step.check_monitor.title"] = ("Check the monitor", "Monitörü kontrol et"),
            ["step.check_monitor.instr"] = ("Look at the vital signs monitor.", "Vital bulgu monitörüne bak."),

            // ---------------------------------------------------------------- mistakes
            ["mistake.notWristband"] = ("That code is not a patient wristband.", "Okunan kod hasta bilekliği değil."),
            ["mistake.wrongPatient"] = ("Wrong patient! This wristband belongs to someone else.", "Yanlış hasta! Bileklik bu hastaya ait değil."),
            ["mistake.wrongIdentity"] = ("You confirmed the wrong patient.", "Yanlış hastanın kimliği doğrulandı."),
            ["mistake.orderWrongPatient"] = ("The request was sent for the wrong patient.", "İstem yanlış hasta için gönderildi."),

            // ---------------------------------------------------------------- PDA
            ["pda.status"] = ("HSE Clinical  ·  Wi-Fi  ·  86%", "HSE Klinik  ·  Wi-Fi  ·  %86"),
            ["pda.title.scan"] = ("Scan Patient", "Hasta Okut"),
            ["pda.title.patient"] = ("Patient Details", "Hasta Bilgileri"),
            ["pda.title.order"] = ("Lab Request", "Laboratuvar İstemi"),
            ["pda.title.sent"] = ("Request Sent", "İstem Gönderildi"),
            ["pda.scan.headline"] = ("Scan the patient wristband", "Hasta bilekliğini okutun"),
            ["pda.scan.help"] = ("Pull the trigger or press SCAN, then point the red laser at the wristband QR code.",
                "Tetiğe veya TARA tuşuna bas, kırmızı lazeri bileklikteki QR koda doğrult."),
            ["pda.scan.last"] = ("Last scanned patient: <b>{0}</b>", "Son okunan hasta: <b>{0}</b>"),
            ["pda.scan.notWristband"] = ("The scanned code is not a patient wristband.", "Okunan kod bir hasta bilekliği değil."),
            ["pda.age"] = ("{0} years  ·  {1}  ·  Blood type {2}", "{0} yaş  ·  {1}  ·  Kan grubu {2}"),
            ["pda.mrn"] = ("Record No", "Protokol No"),
            ["pda.dob"] = ("Date of Birth", "Doğum Tarihi"),
            ["pda.ward"] = ("Ward", "Servis"),
            ["pda.roomBed"] = ("Room / Bed", "Oda / Yatak"),
            ["pda.diagnosis"] = ("Diagnosis", "Tanı"),
            ["pda.physician"] = ("Physician", "Sorumlu Hekim"),
            ["pda.allergy"] = ("ALLERGY:", "ALERJİ:"),
            ["pda.carePlan"] = ("CARE PLAN", "BAKIM PLANI"),
            ["pda.physicianOrder"] = ("PHYSICIAN ORDER", "HEKİM İSTEMİ"),
            ["pda.identityConfirmed"] = ("Identity confirmed", "Kimlik doğrulandı"),
            ["pda.confirmIdentity"] = ("Confirm Identity", "Kimliği Doğrula"),
            ["pda.home"] = ("Home", "Ana Ekran"),
            ["pda.labRequest"] = ("Lab Request", "Tahlil İstemi"),
            ["pda.priority"] = ("Priority", "Öncelik"),
            ["pda.routine"] = ("Routine", "Rutin"),
            ["pda.urgent"] = ("URGENT", "ACİL"),
            ["pda.back"] = ("Back", "Geri"),
            ["pda.send"] = ("Send Request", "İstemi Gönder"),
            ["pda.selectOne"] = ("Select at least one test.", "En az bir tahlil seçin."),
            ["pda.mismatch"] = ("Does not match the physician's order.", "Hekim istemiyle uyuşmuyor."),
            ["pda.missing"] = ("Missing", "Eksik"),
            ["pda.extra"] = ("Extra", "Fazla"),
            ["pda.sent.headline"] = ("Request sent to the laboratory", "İstem laboratuvara iletildi"),
            ["pda.sent.ref"] = ("Reference No", "Referans No"),
            ["pda.sent.next"] = ("Next: prepare and label the sample tubes.", "Sonraki adım: numune tüplerini hazırlayın ve etiketleyin."),

            ["pda.scanButton"] = ("SCAN", "TARA"),
            ["session.next"] = ("Next patient", "Sonraki hasta"),
            ["guide.nextHint"] = ("Press \"Next patient\" on your task list to continue.", "Devam etmek için görev listendeki \"Sonraki hasta\"ya bas."),
            ["pda.tab.patient"] = ("Patient", "Hasta"),
            ["pda.tab.orders"] = ("Orders", "İstemler"),
            ["pda.selected"] = ("Selected tests: {0}", "Seçilen test: {0}"),
            ["checklist.tab"] = ("Tasks {0}/{1}", "Görevler {0}/{1}"),
            ["step.approach.cue2"] = ("Stand here", "Buraya geç"),

            ["sample.blood"] = ("BLOOD", "KAN"),
            ["sample.urine"] = ("URINE", "İDRAR"),
            ["sample.other"] = ("OTHER", "DİĞER"),
        };
    }
}
