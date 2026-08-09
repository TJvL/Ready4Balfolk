# Ready4Balfolk Help

Ready4Balfolk is een wachtrijbeheerapplicatie voor muziek, ontworpen voor balfolk dansevenementen. Het helpt dansers/organisatoren om nummers te beheren per danstype, afspeellijsten samen te stellen en de huidige en komende dansen te tonen op de dansvloer.

---

## Aan de slag

### Muziekmap

Voordat u Ready4Balfolk gebruikt, moet u een muziekmap configureren. Ga naar **Instellingen** en blader naar de map die uw muziekbestanden bevat.

### Bestandsnaamconventie

Nummers worden automatisch gevonden in uw muziekmap. Bestanden moeten dit naampatroon volgen:

**Dans - Artiest - Titel.ext**

Bijvoorbeeld: `Mazurka - Duo Absynthe - La Java Bleue.mp3`

De applicatie splitst de bestandsnaam op koppeltekens om de dansnaam, artiest en titel te extraheren. Ondersteunde audioformaten: MP3, MP2, MP1, WAV, OGG, AIFF en FLAC.

### Indeling van het hoofdscherm

Het hoofdscherm is verdeeld in twee kolommen:

- **Linkerkolom**: Werkbalk, afspeelknoppen, de equalizer en de wachtrij- of geschiedenisweergave
- **Rechterkolom**: Nummercatalogus of danslijst

U kunt schakelen tussen weergaven in elke kolom met behulp van de schakelknoppen bovenaan elk paneel.

---

## Werkbalk

De werkbalk bevindt zich linksboven in het hoofdscherm en biedt navigatie naar de hoofdsecties van de applicatie.

### Exit

Sluit de applicatie.

### Help

Opent dit helpscherm.

### Instellingen

Opent het instellingenscherm waar u muziekmap, wachtrijgedrag, presentatieschermen en thema kunt configureren.

---

## Afspelen

Het afspeelpaneel toont wat er momenteel wordt afgespeeld en biedt bedieningsknoppen.

### Nu afspelen-weergave

- **Dansnaam**: Prominent weergegeven bovenaan
- **Artiest en titel**: Weergegeven onder de dansnaam als "Artiest — Titel"
- **Voortgangsbalk**: Toont de huidige positie in het nummer, met verstreken tijd links en totale duur rechts

Wanneer een berichtitem wordt afgespeeld, schakelt de weergave over naar berichtmodus met automatisch scrollende tekst.

### Bedieningsknoppen

- **Play / Pause**: Schakelt afspelen in/uit. Wanneer een nummer is geladen, klik om het afspelen te starten of te pauzeren.
- **Herstarten**: Start het huidige nummer opnieuw vanaf het begin. Als een nummer momenteel wordt afgespeeld, verschijnt eerst een bevestigingsdialoog (tenzij uitgeschakeld in instellingen).
- **Next / Clear**: Deze knop verandert van gedrag afhankelijk van de wachtrijstatus:
  - **Next** (wanneer de wachtrij items bevat): Slaat over naar het volgende item in de wachtrij. Een bevestigingsdialoog verschijnt als een nummer momenteel wordt afgespeeld (tenzij uitgeschakeld in instellingen).
  - **Clear** (wanneer de wachtrij leeg is): Stopt het afspelen en wist het huidige item.

---

## Equalizer

De equalizer vormt het geluid dat naar de PA gaat. Hij is bedoeld voor avonden waarop de app de
enige plek is waar het geluid bijgesteld kan worden: geen mengtafel, geen geluidsman. Stel hem
vroeg op de avond in op de zaal en laat hem daarna met rust.

Hij is standaard ingeklapt. De kop toont **aan** of **uit**, zodat een equalizer die van een vorige
avond aan is blijven staan zichtbaar is zonder het paneel te openen. Wijzigingen werken direct,
ook tijdens het afspelen, wat de enige praktische manier is om een zaal te beoordelen.

### Banden

Zeven schuifregelaars, die elk tot 15 dB verzwakken of versterken op een vaste frequentie:

| Regelaar | Meestal gebruikt voor |
|---|---|
| 63 Hz | Gewicht en gerommel |
| 160 Hz | Dreun en holheid, het gebruikelijke probleem in een zaal met harde wanden |
| 400 Hz | Modderigheid |
| 1 kHz | Body van accordeon, viool en stem |
| 2,5 kHz | Aanwezigheid en aanzet |
| 6,3 kHz | Schelheid, sisklanken |
| 16 kHz | Lucht en glans |

De buitenste twee zijn shelving-filters, dus die tillen of verlagen alles onder 63 Hz en boven
16 kHz in plaats van alleen een band rond het middelpunt. Verzwakken is bijna altijd veiliger dan
versterken.

### Laagafsnijding

Een hoogdoorlaatfilter dat alles onder de gekozen frequentie weghaalt, instelbaar van 20 tot
200 Hz. Nuttig tegen gerommel van het podium, hanteringsgeluid en verkeer, en op kleine speakers
die de onderste octaaf toch niet zinvol kunnen weergeven. Begin rond 40 a 60 Hz.

### Voorversterking

Banden versterken maakt het signaal luider en kan de uitgang laten clippen, wat klinkt als
vervorming die erger wordt bij de luidste stukken van een nummer. Heb je iets versterkt, draai de
voorversterking dan ongeveer net zoveel omlaag als je grootste versterking.

### Terug naar vlak

Zet elke regelaar terug op 0 dB en schakelt de laagafsnijding uit. De equalizer blijft ingeschakeld.

Als het paneel meldt dat de equalizer niet beschikbaar is, kon de BASS_FX-audiobibliotheek niet
geladen worden. Het afspelen wordt daar niet door beinvloed.

---

## Wachtrij

De wachtrij toont de komende items die zullen worden afgespeeld, in volgorde van boven naar beneden.

### Wachtrijitems

De wachtrij kan verschillende soorten items bevatten, elk met een uniek uiterlijk:

- **Nummer**: Een muziekbestand om af te spelen. Toont de dansnaam, artiest, titel en duur.
- **Auto-nummer**: Een willekeurig geselecteerd nummer, getoond met een vervaagd uiterlijk en een recyclingsicoon. Auto-nummers verschijnen wanneer de functie voor automatisch toevoegen is ingeschakeld en de wachtrij leeg is. Ze hebben twee extra acties:
  - **Vernieuwen**: Kies een ander willekeurig nummer
  - **Vastmaken**: Converteer het auto-nummer naar een gewoon nummer en behoud het permanent in de wachtrij
- **Stop**: Een markering waar het afspelen pauzeert totdat u handmatig doorgaat. Getoond met een oranje markering.
- **Pauze**: Een getimede pauze. Het afspelen wordt automatisch hervat na de geconfigureerde duur. Getoond met een blauwe markering.
- **Bericht**: Een tekstannonce die op het scherm wordt weergegeven, optioneel met een duur. Getoond met een turquoise markering.

### De wachtrij beheren

- **Herschikken**: Sleep items om ze te herschikken
- **Verwijderen**: Selecteer een item en druk op de Delete-toets, of gebruik de knop Verwijderen in de werkbalk
- **Dubbelklik op een nummer** in de Nummercatalogus om het toe te voegen aan de wachtrij

### Wachtrijwerkbalk

De werkbalk boven de wachtrij biedt deze acties:

- **Schakel naar Geschiedenis**: Schakel het linkerpaneel om de geschiedenisweergave te tonen
- **Willekeurig nummer in wachtrij plaatsen**: Voeg een willekeurig geselecteerd nummer toe, getrokken uit de labels die op dat moment in de pool zitten. Zonder keuze trekt hij uit elke dans waarvan je een nummer hebt.
- **Stop in wachtrij plaatsen**: Voeg een stopmarkering toe aan de wachtrij
- **Pauze in wachtrij plaatsen**: Voeg een pauzemarkering toe met de duur die is geconfigureerd in instellingen
- **Bericht aanvragen**: Opent een dialoog waar u een bericht kunt typen en optioneel een duur kunt instellen
- **Geselecteerde verwijderen**: Verwijder het momenteel geselecteerde wachtrijitem
- **Wachtrij wissen**: Verwijder alle items uit de wachtrij (met bevestiging)

### Statusbalk

Onderaan het wachtrijpaneel:

- **Aantal items**: Toont het aantal items in de wachtrij, of "Wachtrij leeg"
- **Eindtijd**: Geschatte tijd waarop de afspeellijst zal eindigen, weergegeven als "Afspeellijst eindigt om HH:mm". Als de wachtrij een stop of een bericht zonder duur bevat, toont het "stopt om" in plaats daarvan, aangezien het afspelen op dat punt zal pauzeren.

---

## Geschiedenis

De geschiedenisweergave toont een logboek van items die zijn afgespeeld of overgeslagen tijdens de huidige sessie.

### Geschiedenis bekijken

Schakel naar de geschiedenisweergave met behulp van de schakelknop in de wachtrijwerkbalk. Elk item toont:

- **Beschrijving**: De dansnaam (voor nummers), berichttekst, pauze of stop
- **Duur**: Hoe lang het item is afgespeeld
- **Status**: Of het voltooid of overgeslagen is

### Geschiedeniswerkbalk

- **Schakel naar Wachtrij**: Ga terug naar de wachtrijweergave
- **Geschiedenis exporteren**: Sla de geschiedenis op in een CSV-bestand. Handig voor het bijhouden van wat er is afgespeeld tijdens een evenement.
- **Geschiedenis wissen**: Verwijder alle geschiedenisvermeldingen (met bevestiging)

### Statusbalk

Onderaan het geschiedenispaneel:

- **Aantal items**: Aantal geschiedenisvermeldingen, of "Geen geschiedenis"
- **Totale duur**: Gecombineerde afspeeltijd van alle geschiedenisvermeldingen

---

## Nummercatalogus

De nummercatalogus toont alle gevonden nummers in een doorzoekbare, sorteerbare tabel.

### Nummers bladeren

De catalogus toont nummers in een gegevensraster met deze kolommen:

- **Dans**: Het danstype
- **Artiest**: De naam van de artiest of band
- **Titel**: De titel van het nummer
- **Lengte**: De duur van het nummer in MM:SS-formaat

Klik op een kolomkop om op die kolom te sorteren. Klik opnieuw om de sorteervolgorde om te draaien.

### Zoeken

Gebruik het zoekvak in de werkbalk om nummers te filteren. De zoekopdracht zoekt gelijktijdig in de dansnaam, artiest en titel. Resultaten worden in realtime bijgewerkt terwijl u typt. Klik op de knop Wissen om de zoekopdracht te resetten.

### Nummers in wachtrij plaatsen

Dubbelklik op een nummer om het toe te voegen aan het einde van de wachtrij. Als duplicaatpreventie is ingeschakeld in instellingen, kunnen nummers die momenteel worden afgespeeld, al in de wachtrij staan of al in de geschiedenis staan niet opnieuw worden toegevoegd.

### Schakel naar Danslijst

Gebruik de schakelknop in de werkbalk om het rechterpaneel om te schakelen naar de danslijst.

---

## Danslijst

De danslijst is elke balfolkdans en elke naam die zo'n dans heeft. Hij komt van
[BigBalfolkList](https://tjvl.github.io/BigBalfolkList/) en Ready4Balfolk gebruikt hem precies zoals
hij gepubliceerd is: er valt niets op te bouwen, niets in te vullen, en niets in de applicatie
bewerkt hem.

Er wordt een kopie met Ready4Balfolk meegeleverd, dus de eerste keer werkt het ook zonder internet.
Bij elke start wordt gekeken of er een nieuwere is.

### Wat erin staat

- **De namen van een dans zijn gelijkwaardig.** Over spelling wordt getwist en deze lijst kiest geen
  partij; de eerste naam is simpelweg degene die de applicatie toont. Een naam hoort bij precies een
  dans, en dat is wat Ready4Balfolk in staat stelt om een dans te noemen als hij een naam in een
  bestandsnaam herkent.
- **Al het andere is een label**: waar een dans vandaan komt, tot welke familie hij hoort, of hij in
  een suite gedanst wordt. Een dans kan Bretons *en* een gavotte *en* deel van een suite zijn zonder
  onder een daarvan gearchiveerd te worden.

### Kiezen waar willekeurig uit getrokken wordt

De labels in de linkerkolom vormen de **pool**: klik een label om het erin te zetten, klik nog eens
om het eruit te halen. Een willekeurige keuze, en de automatische wachtrij, trekken uit de dansen die
een van de labels in de pool dragen. Zonder keuze is de pool elke dans.

De werkbalk zegt altijd waaruit getrokken wordt, want een label is zo aangeklikt en daarna niet meer
op te merken. **Alles** maakt de pool weer leeg.

Labels worden getoond op grootte van het aantal dansen dat ze draagt, en een label op een kaart
aanklikken doet hetzelfde als een label in de kolom.

### Een bepaalde dans

Klik op de **dobbelsteen** bij een dans om een willekeurig nummer van die dans in de wachtrij te
zetten, wat de pool ook is. Bij een dans waarvan je geen nummers hebt staat dat er in plaats daarvan,
en zo'n dans kan ook nooit uit een willekeurige keuze komen.

### Zoeken

Het zoekveld doorzoekt elke schrijfwijze van elke dans, ongeacht hoofdletters, accenten en
leestekens, dus `hanterdro` vindt *Hanter dro*.

### Bijhouden

- **Bijwerken**: haalt de lijst op zoals BigBalfolkList hem op dit moment publiceert. Handig als je
  weet dat er net iets aan toegevoegd is.
- **Uit een bestand**: neemt de lijst over uit een `dances.json` op deze computer, voor een machine
  die nooit online komt.

In beide gevallen wordt de lijst in zijn geheel vervangen en eerst gecontroleerd; als hij niet te
lezen is, blijft die je al had in gebruik.

### Iets dat ontbreekt of verkeerd gespeld is?

Stel het voor op [BigBalfolkList](https://tjvl.github.io/BigBalfolkList/). Op die site kun je een
schrijfwijze toevoegen of verbeteren, een dans van een label voorzien of een ontbrekende dans
toevoegen, en wat je gedaan hebt wordt een voorstel waar iemand naar kijkt. Iedereen die de lijst
gebruikt krijgt jouw correctie, en dat is nou juist waarom er een lijst is.

---

## Instellingen

Het instellingenscherm laat u applicatiegedrag configureren. Alle wijzigingen worden automatisch opgeslagen.

### Muziekmap

Het pad naar de map die uw muziekbestanden bevat. Klik op **Bladeren** om een map te selecteren. De applicatie scant deze map recursief op audiobestanden en extraheert dans, artiest en titel uit bestandsnamen.

### Maximaal aantal wachtrijitems

Het maximale aantal items dat is toegestaan in de wachtrij, tussen 1 en 100. Wanneer de wachtrij vol is, kunnen geen nieuwe items worden toegevoegd totdat bestaande items zijn afgespeeld of verwijderd.

### Pauzeduur

De standaardduur (in seconden) voor pauzemarkering die aan de wachtrij wordt toegevoegd, tussen 1 en 300 seconden. Deze waarde wordt gebruikt wanneer u op "Pauze in wachtrij plaatsen" klikt in de wachtrijwerkbalk.

### Presentatieschermen

Het aantal presentatievensters dat moet worden getoond, tussen 0 en 10. Stel in op 0 om presentatievensters volledig uit te schakelen. Presentatievensters zijn ontworpen om te worden getoond op projectoren of externe schermen die zichtbaar zijn voor dansers.

### Automatisch willekeurig nummer toevoegen

Wanneer ingeschakeld, wordt een willekeurig nummer automatisch toegevoegd aan de wachtrij wanneer deze leeg wordt tijdens het afspelen. Het auto-nummer verschijnt met een vervaagde stijl en kan worden vernieuwd (om een ander nummer te kiezen) of vastgemaakt (om het permanent te behouden). Auto-nummers worden automatisch verwijderd wanneer u handmatig items aan de wachtrij toevoegt.

### Dubbele nummers toestaan

Wanneer uitgeschakeld, kunnen nummers die momenteel worden afgespeeld, al in de wachtrij staan of al in de sessiegeschiedenis staan niet opnieuw worden toegevoegd aan de wachtrij. Dit voorkomt dat hetzelfde nummer twee keer wordt afgespeeld in een sessie.

### Afspeelacties bevestigen

Wanneer ingeschakeld (de standaard), wordt een bevestigingsdialoog getoond voordat wordt overgeslagen naar het volgende item, het huidige nummer wordt gewist of het afspelen opnieuw wordt gestart. Schakel dit uit als u de bevestigingen storend vindt tijdens een optreden.

### Thema

Kies tussen drie opties:

- **Automatisch**: Volgt het systeemthema
- **Licht**: Licht kleurenschema
- **Donker**: Donker kleurenschema

### Logbestand exporteren

Klik om het applicatielogbestand op te slaan. Handig voor het oplossen van problemen of het indienen van bugrapportages.

---

## Presentatiescherm

Presentatievensters zijn volledige schermweergaven bedoeld voor projectoren of externe monitoren, die het publiek tonen wat er momenteel wordt afgespeeld en wat er hierna komt.

### Indeling

- **Bovenste helft**: De huidige dansnaam in grote tekst, met artiest en titel eronder. Wanneer een berichtitem actief is, wordt de berichttekst in plaats daarvan weergegeven.
- **Voortgangsbalk**: Een groene balk in het midden die de afspeelvoortgang toont
- **Onderste helft**: De volgende komende dansnaam en nummerdetails, of "Geen volgend nummer" als de wachtrij leeg is

### Configuratie

Stel het aantal presentatievensters in via **Instellingen > Presentatieschermen**. Elk venster kan naar een ander scherm worden verplaatst en onthoudt zijn positie tussen sessies.
