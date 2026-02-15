# Ready4Balfolk Help

Ready4Balfolk is een wachtrijbeheerapplicatie voor muziek, ontworpen voor balfolk dansevenementen. Het helpt dansers/organisatoren om nummers te beheren per danstype, afspeellijsten samen te stellen en de huidige en komende dansen te tonen op de dansvloer.

---

## Aan de slag

### Muziekmap

Voordat u Ready4Balfolk gebruikt, moet u een muziekmap configureren. Ga naar **Instellingen** en blader naar de map die uw muziekbestanden bevat.

### Bestandsnaamconventie

Nummers worden automatisch gevonden in uw muziekmap. Bestanden moeten dit naampatroon volgen:

**Dans - Artiest - Titel.mp3**

Bijvoorbeeld: `Mazurka - Duo Absynthe - La Java Bleue.mp3`

De applicatie splitst de bestandsnaam op koppeltekens om de dansnaam, artiest en titel te extraheren. Ondersteunde audioformaten zijn onder andere MP3, FLAC, WAV, OGG en andere formaten die worden ondersteund door de BASS-audiobibliotheek.

### Indeling van het hoofdscherm

Het hoofdscherm is verdeeld in twee kolommen:

- **Linkerkolom**: Werkbalk, afspeelknoppen en de wachtrij- of geschiedenisweergave
- **Rechterkolom**: Nummercatalogus of dansboom-editor

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

### Danssynoniemen

Opent de danssynoniemen-editor, waar u alternatieve namen voor dansen kunt definiëren om nummers onder één danstype te groeperen.

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
- **Willekeurig nummer in wachtrij plaatsen**: Voeg een willekeurig geselecteerd nummer toe op basis van de huidige gemarkeerde selectie in de dansboom. De willekeurige selectie respecteert gewichten en bereik.
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

### Schakel naar Dansboom

Gebruik de schakelknop in de werkbalk om het rechterpaneel om te schakelen naar de dansboom-editor.

---

## Dansboom

De dansboom biedt een hiërarchische weergave van danscategorieën, gebruikt om dansen te organiseren en willekeurige nummerselectie te regelen.

### Structuur

De boom is georganiseerd in categorieën (takken) en dansen (bladeren):

- **Categorieën** kunnen andere categorieën en dansen bevatten, waardoor een hiërarchie wordt gevormd
- **Dansen** zijn de bladknopen, die een specifiek danstype vertegenwoordigen
- Elk item toont zijn naam en het aantal overeenkomende nummers tussen haakjes, bijv. "Mazurka (42)"

### Markeren voor willekeurige selectie

Klik op het dobbelsteen-icoon naast elk item om het te **markeren** voor willekeurige selectie. Het gemarkeerde item bepaalt het bereik bij gebruik van "Willekeurig nummer in wachtrij plaatsen":

- **Markeer de hoofdmap**: Willekeurige selectie kiest uit alle dansen in de hele boom
- **Markeer een categorie**: Willekeurige selectie kiest uit alle dansen in die categorie en zijn subcategorieën
- **Markeer een enkele dans**: Willekeurige selectie kiest alleen nummers voor die specifieke dans

Het gemarkeerde item wordt gemarkeerd om te tonen dat het actief is.

### Gewichten

Elke categorie en dans heeft een **gewicht**-waarde die de kans op willekeurige selectie beïnvloedt. Een hoger gewicht betekent dat dat item eerder wordt gekozen. De effectieve kans dat een dans wordt geselecteerd is evenredig aan zijn gewicht vermenigvuldigd met zijn aantal beschikbare nummers.

Om gewichten te bewerken, selecteert u een item en klikt u op de bewerkknop. Een numerieke spinner verschijnt naast de naam waar u het gewicht kunt aanpassen.

### De boom bewerken

Selecteer een item om actieknoppen te tonen:

- **Categorie toevoegen**: Maak een nieuwe subcategorie binnen de geselecteerde categorie
- **Dans toevoegen**: Maak een nieuwe dans binnen de geselecteerde categorie
- **Bewerken**: Ga naar bewerkingsmodus om het item te hernoemen en zijn gewicht aan te passen
- **Bevestigen**: Sla wijzigingen op wanneer u in bewerkingsmodus bent
- **Verwijderen**: Verwijder het item (en al zijn kinderen, als het een categorie is)
- **Annuleren**: Verwerp wijzigingen wanneer u in bewerkingsmodus bent

### Werkbalk

- **Schakel naar Nummerlijst**: Ga terug naar de nummercatalogusweergave
- **Ongedaan maken** (Ctrl+Z): Maak de laatste bewerking ongedaan. Beweeg de muis over de knop om een beschrijving te zien van de actie die ongedaan wordt gemaakt.
- **Opnieuw uitvoeren** (Ctrl+Y): Voer de laatst ongedaan gemaakte bewerking opnieuw uit. Beweeg de muis over de knop om een beschrijving te zien.
- **Importeren**: Laad een dansboom uit een JSON-bestand, waarbij de huidige boom wordt vervangen
- **Exporteren**: Sla de huidige boom op in een JSON-bestand voor back-up of delen

---

## Danssynoniemen

De danssynoniemen-editor laat u alternatieve namen voor dansen definiëren. Wanneer de dansnaam van een nummer overeenkomt met een synoniem, wordt het gegroepeerd onder de hoofddansnaam. Dit is handig wanneer uw muziekcollectie verschillende spellingen of regionale namen gebruikt voor dezelfde dans.

Vermeldingen worden weergegeven als kaarten in een vloeiende meerkolommen-indeling. Elke kaart toont de hoofddansnaam bovenaan en zijn synoniemen als tags eronder.

### Vermeldingen beheren

- **Toevoegen**: Klik op de **+**-knop in de werkbalk om een nieuwe vermelding te maken. De vermelding wordt gemaakt met een standaardnaam en gaat direct naar bewerkingsmodus zodat u de naam kunt typen.
- **Naam bewerken**: Klik op het **potlood**-icoon op een kaart om naar bewerkingsmodus te gaan. De naam wordt een bewerkbaar tekstveld, gefocust en volledig geselecteerd. Tijdens het bewerken zijn alle andere kaarten uitgeschakeld.
  - Druk op **Enter** of klik op het **vinkje**-icoon om het hernoemen te bevestigen.
  - Druk op **Escape** of klik op het **X**-icoon om te annuleren en terug te keren naar de oorspronkelijke naam.
  - Het annuleren van een nieuw toegevoegde vermelding maakt de toevoeging volledig ongedaan.
- **Verwijderen**: Klik op het **prullenbak**-icoon op een kaart (alleen zichtbaar wanneer niet in bewerkingsmodus) om de vermelding en al zijn synoniemen te verwijderen.

### Synoniemen beheren

Synoniemen verschijnen als tags onder de hoofddansnaam.

- **Synoniem toevoegen**: Klik op de **+**-knop aan het einde van de synoniem-tags om een inline tekstveld te tonen. Tijdens het toevoegen zijn alle andere kaarten uitgeschakeld.
  - Typ het synoniem en druk vervolgens op **Enter** of klik op het **vinkje**-icoon om te bevestigen.
  - Druk op **Escape** of klik op het **X**-icoon om te annuleren.
- **Synoniem verwijderen**: Klik op de **X**-knop op een synoniem-tag om deze te verwijderen.

### Werkbalk

- **Terug**: Keer terug naar het hoofdscherm
- **Ongedaan maken** (Ctrl+Z): Maak de laatste wijziging ongedaan. De knopinfo toont welke actie ongedaan wordt gemaakt.
- **Opnieuw uitvoeren** (Ctrl+Y): Voer de laatst ongedaan gemaakte wijziging opnieuw uit. De knopinfo toont welke actie opnieuw wordt uitgevoerd.
- **Importeren**: Laad synoniemen uit een JSON-bestand, waarbij de huidige set wordt vervangen (met bevestiging)
- **Exporteren**: Sla de huidige synoniemen op in een JSON-bestand voor back-up of delen
- **Toevoegen**: Maak een nieuwe dans

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
