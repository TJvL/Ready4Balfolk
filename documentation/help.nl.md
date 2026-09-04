# Ready4Balfolk Help

Ready4Balfolk is een wachtrijbeheerprogramma voor balfolkavonden met opgenomen muziek. Het helpt dansers en organisatoren nummers per dans te beheren, afspeellijsten op te bouwen en de huidige en volgende dans aan de zaal te tonen.

---

## Aan de slag

### Muziekmap

Ready4Balfolk vraagt bij de eerste start om één map, in een korte setup die ook de danslijst
ophaalt en laat zien wat er op je wacht. Alles onder die map telt mee, hoe hij ook is ingedeeld. Je
kunt de setup later opnieuw uitvoeren vanuit **Instellingen**.

### Hoe je bestanden gelezen worden

Nummers worden automatisch ontdekt in je muziekmap. Er is **geen verplichte naamconventie**, en er wordt niets aangenomen over hoe je bibliotheek is ingedeeld: losse bestanden in één map en een boom van vijf niveaus diep zijn allebei gewoon.

- **De dans** wordt herkend wanneer een naam uit je danslijst ergens in de bestandsnaam of in de tags staat: `10. Hep Harz (Cercle).mp3`, `11-La Violette - valse 5tps.mp3`, of een dans die in de tags is geschreven. Twee bronnen die het eens zijn maken een antwoord betrouwbaar, en als een bestand twee dansen noemt zonder iets dat ze scheidt, wordt er niets aangenomen.
- **De artiest** komt uit de artiesttags. Een mapnaam wordt niet als artiest gelezen: hetzelfde niveau is in de ene bibliotheek een artiest en in de volgende een land.
- **De titel** komt uit de titeltag, of anders uit de bestandsnaam met een eventueel volgnummer eraf.

Alles wat zo niet beantwoord is, wacht op jou in **Review** in plaats van met een gok te worden ingevuld. Een nummer staat in je bibliotheek of in review, nooit allebei: de oversteek vraagt een artiest, een titel, een dans uit de gepubliceerde lijst, en jouw akkoord op alle drie. Een niet-gereviewde bibliotheek toont terecht geen muziek.

Review is een vaste plek, geen stap van de setup. Wijzig tags, hernoem een bestand of zet er nieuwe bij en ze komen vanzelf terug, met wat je eerder antwoordde behouden.

Ondersteunde audioformaten: MP3, MP2, MP1, WAV, OGG, AIFF en FLAC.

### Regels: vertellen hoe je bestanden heten

Als je bibliotheek *wél* een vaste vorm heeft, kun je dat zeggen. Open **Review** en klik op **Regels**; het paneel opent boven de wachtrij die het moet legen. Wat je daar verklaart gaat boven alles wat het programma zelf heeft uitgedokterd, want jij kent je bibliotheek en het programma niet.

Elk van de vier zet je apart aan, en ze staan allemaal uit. Vink aan wat jouw bibliotheek daadwerkelijk gebruikt; de rest blijft dichtgeklapt en doet niets, zodat een regel die je niet ziet je bestanden nooit raakt. Iets uitvinken laat staan wat je erin hebt gezet, voor het geval je het terug wilt.

- **Bestandsnaampatronen.** `%d` dans, `%a` artiest, `%t` titel, `%n` volgnummer, `%i` negeren, `%ex` extensie; al het andere moet er letterlijk staan. `%d - %a - %t` leest `Mazurka - Naragonia - Idiosyncrasie.mp3`. Een patroon moet een hele naam dekken, en het eerste patroon in de lijst dat past is het patroon dat antwoordt, dus zet het meest specifieke bovenaan.
- **Mapniveaus.** Geteld van buiten naar binnen. Niveau 1 als artiest bestempelen leest die map als artiest voor elk bestand dat diep genoeg zit, en zegt niets over de bestanden die dat niet zijn.
- **Tags.** Welke tagvelden welke waarde bevatten. Standaard gelden artiest en albumartiest als artiest, het titelveld als titel, en geldt geen enkele tag als dans. Een dansnaam uit je lijst wordt hoe dan ook in elke tag herkend, wat je hier ook instelt.
- **Een eigen danstag.** Sommige bibliotheken dragen de dans in een vrije tag van zichzelf: een ID3v2 `TXXX`-frame of een Xiph-veld dat `DANCE`, `STYLE` of hoe je tagger het ook noemde heet. Geef die tagnaam hier op en de waarde wordt als dans gelezen, in zijn geheel: een waarde die de lijst niet kent parkeert het nummer, precies als elk ander verklaard antwoord. Het paneel laat zien op hoeveel van je bestanden zo'n tag staat voordat je je eraan verbindt.

**Een regel is een bulkgoedkeuring**, en dat is precies de bedoeling: in plaats van tweeduizend bestanden één voor één te beantwoorden, ga je één keer akkoord met de regel die ze beantwoordt. Daarom zie je vooraf wat hij doet, in de getallen die ertoe doen: hoeveel bestanden hij pakt, wat hij ervan maakt, en hoeveel er overblijven. Een regel toevoegen, verwijderen of herordenen leest je bibliotheek opnieuw, want een regel is er om de bestanden te beantwoorden die er al liggen.

**Het vertelt je ook hoe je bibliotheek eruitziet.** Bovenin het paneel staan de vormen die uit je eigen bestandsnamen en mappen gemeten zijn: "296 van 2685 bestanden hebben de vorm `%d - %i - %t`", "niveau 1 lijkt de artiest, 96 van 121 zijn het eens", elk met de tellingen erachter en de bestanden waaruit het gelezen is. Het zijn voorstellen: er gebeurt niets tot je op **Verklaar het** klikt. Waar de metingen het niet eens zijn wordt de vorm getoond en niets benoemd, want een zelfverzekerde gok over je hele bibliotheek is erger dan geen gok.

**Een dans die in de gepubliceerde lijst ontbreekt is geen regelprobleem.** Het paneel linkt bovenaan naar [BigBalfolkList](https://tjvl.github.io/BigBalfolkList/), want daar wordt een ontbrekende dans voorgesteld. Onder de regels staat een schakelaar om een dans die de lijst niet kent tóch je bibliotheek in te laten. Hij staat uit om mee te beginnen: de gedeelde lijst is wat een dansnaam voor iedereen hetzelfde laat betekenen, en een nummer dat zo binnenkomt kan nooit in een willekeurige keuze opduiken, want die trekt per tag en een dans die niemand publiceerde heeft er geen.

### Indeling van het hoofdscherm

Het hoofdscherm bestaat uit twee kolommen:

- **Linkerkolom**: werkbalk, afspeelbediening, de equalizer, en de wachtrij- of geschiedenisweergave
- **Rechterkolom**: nummercatalogus of danslijst

Met de schakelknoppen bovenaan elk paneel wissel je per kolom van weergave.

---

## Werkbalk

De werkbalk zit linksboven op het hoofdscherm en leidt naar de hoofdonderdelen van het programma.

### Exit

Sluit het programma.

### Help

Opent dit helpscherm.

### Instellingen

Opent het instellingenscherm: wachtrijgedrag, presentatieschermen, thema, en de weg terug naar de
setup.

### Review

Alles wat op jou wacht, met een teller van hoeveel nummers dat zijn. Niets bereikt je bibliotheek zonder een artiest, een titel, een dans uit de gepubliceerde lijst en jouw akkoord, dus dit is waar een bibliotheek gemaakt wordt in plaats van een klusje aan het eind van een. Zie [Review](#review-1) hieronder.

---

## Afspelen

Het afspeelpaneel toont wat er speelt en biedt de bedieningsknoppen.

### Nu spelen-weergave

- **Dansnaam**: prominent bovenaan
- **Artiest en titel**: daaronder als "Artiest: Titel"
- **Voortgangsbalk**: de positie in het nummer, met verstreken tijd links en totale duur rechts

Speelt er een bericht, dan schakelt de weergave naar berichtmodus met automatisch scrollende tekst.

### Bedieningsknoppen

- **Afspelen / Pauze**: schakelt het afspelen. Met een geladen nummer klik je om te starten of te pauzeren.
- **Herstart**: begint het huidige nummer opnieuw. Speelt er iets, dan komt eerst een bevestiging (tenzij uitgeschakeld in de instellingen).
- **Volgende / Wissen**: deze knop verandert met de toestand van de wachtrij:
  - **Volgende** (wachtrij met items): gaat naar het volgende item. Een bevestiging verschijnt als er iets speelt (tenzij uitgeschakeld).
  - **Wissen** (lege wachtrij): stopt het afspelen en maakt het huidige item leeg.

---

## Equalizer

De equalizer vormt het geluid dat naar de PA gaat. Hij bestaat voor de avonden waarop het programma
de enige plek is waar het geluid bijgesteld kan worden: geen mengtafel, geen geluidstechnicus. Stel
hem vroeg op de avond af op de zaal en blijf er daarna vanaf.

Hij is standaard ingeklapt. De kop toont **aan** of **uit**, zodat een equalizer die van een vorige
avond nog aanstaat zichtbaar is zonder het paneel te openen. Wijzigingen werken meteen, ook terwijl
een nummer speelt, de enige praktische manier om een zaal te beoordelen.

### Banden

Zeven schuiven, elk tot 15 dB versterking of verzwakking op een vaste frequentie:

| Schuif | Meestal voor |
|---|---|
| 63 Hz | Gewicht en gerommel |
| 160 Hz | Dreun en dofheid, hét probleem in een zaal met harde muren |
| 400 Hz | Modderigheid |
| 1 kHz | Het lijf van accordeon, viool en stem |
| 2,5 kHz | Aanwezigheid en aanslag |
| 6,3 kHz | Scherpte, sisklanken |
| 16 kHz | Lucht en glans |

De buitenste twee zijn shelvingfilters: ze tillen of dempen alles onder 63 Hz en boven 16 kHz in
plaats van alleen een band rond het midden. Verzwakken is vrijwel altijd veiliger dan versterken.

### Laagafsnijding

Een hoogdoorlaatfilter dat alles onder de gekozen frequentie weghaalt, instelbaar van 20 tot
200 Hz. Nuttig tegen podiumgerommel, aanraakgeluid en verkeer, en op kleine luidsprekers die het
onderste octaaf toch niet zinvol weergeven. Begin rond 40 tot 60 Hz.

### Voorversterking

Banden versterken maakt het signaal luider en kan de uitgang laten clippen, dat klinkt als
vervorming die erger wordt op de luidste stukken. Heb je iets versterkt, trek de voorversterking
dan ongeveer je grootste versterking omlaag.

### Terug naar vlak

Zet elke schuif op 0 dB en schakelt de laagafsnijding uit. De equalizer zelf blijft aan.

Meldt het paneel dat de equalizer niet beschikbaar is, dan kon de BASS_FX-audiobibliotheek niet
geladen worden. Het afspelen zelf heeft er geen last van.

---

## Wachtrij

De wachtrij toont wat er gaat komen, in volgorde van boven naar beneden.

### Wachtrijitems

De wachtrij kan verschillende soorten items bevatten, elk met een eigen uiterlijk:

- **Nummer**: een muziekbestand. Toont dans, artiest, titel en duur.
- **Auto-nummer**: een willekeurig gekozen nummer, vervaagd weergegeven met een recycle-icoon. Het staat onderaan de wachtrij, onder de verzoeken, zolang de auto-wachtrij aanstaat en er iets speelt. Het heeft twee extra acties:
  - **Vernieuwen**: kies een ander willekeurig nummer
  - **Vastzetten**: maak van het auto-nummer een gewoon nummer dat blijft staan
- **Stop**: een markering waar het afspelen pauzeert tot jij verdergaat. Oranje gemarkeerd.
- **Pauze**: een getimede onderbreking. Het afspelen gaat vanzelf verder na de ingestelde duur. Blauw gemarkeerd.
- **Bericht**: een tekstmededeling op het scherm, eventueel met een duur. Teal gemarkeerd.
- **Einde van de avond**: de muziek waarmee het bal afgelopen is, paars gemarkeerd. Het is geen nummer: het is het bestand uit de instellingen, het komt nooit in je bibliotheek, en er gaat niets meer achter in de wachtrij. Haal je het weg, dan is de avond weer open.

### De wachtrij beheren

- **Herordenen**: sleep items naar een andere plek
- **Verwijderen**: selecteer een item en druk op Delete, of gebruik de knop in de werkbalk
- **Dubbelklik een nummer** in de catalogus om het achteraan toe te voegen
- **Rechtsklik een nummer** en kies **Nummer bewerken** om zijn dans, artiest of titel meteen te corrigeren: zie [de catalogus](#nummercatalogus)

### Wachtrijwerkbalk

De werkbalk boven de wachtrij biedt:

- **Naar geschiedenis**: toont links de geschiedenisweergave
- **Willekeurig nummer**: voegt een willekeurig gekozen nummer toe, getrokken uit de tags die nu in de pool staan. Zonder keuze trekt hij uit elke dans waar je een nummer van hebt.
- **Stop toevoegen**: zet een stopmarkering in de wachtrij
- **Pauze toevoegen**: zet een pauze met de duur uit de instellingen in de wachtrij
- **Bericht toevoegen**: opent een venster voor een bericht met eventueel een duur
- **Avond afsluiten**: zet de slotmuziek in de wachtrij. Uit zolang er geen bestand in de instellingen staat, en zolang er al een in de wachtrij staat of speelt.
- **Selectie verwijderen**: haalt het geselecteerde item weg
- **Wachtrij wissen**: haalt alles weg (met bevestiging)

### Statusbalk

Onderaan het wachtrijpaneel:

- **Aantal items**: het aantal in de wachtrij, of "Wachtrij leeg"
- **Eindtijd**: wat de wachtrij gaat doen. Staat de auto-wachtrij uit, dan is het de geschatte tijd waarop de lijst klaar is, als "Afspeellijst eindigt om HH:mm". Staat hij aan, dan bestaat dat moment niet, want de wachtrij vult zichzelf steeds aan: heb je een eindtijd ingesteld, dan staat die er, als "Afspeellijst loopt af om HH:mm", en anders staat er dat de lijst doorgaat tot jij hem stopt. Een stop of een bericht zonder duur gaat daar overheen met "stopt om", want daar pauzeert het afspelen. Staat het einde van de avond in de wachtrij, dan heeft de avond weer een echt einde en staat er weer "eindigt om".

---

## Geschiedenis

De geschiedenis is het logboek van wat er deze avond gespeeld of overgeslagen is.

### Geschiedenis bekijken

Schakel naar de geschiedenis met de knop in de wachtrijwerkbalk. Elke regel toont:

- **Omschrijving**: de dansnaam (bij nummers), de berichttekst, de pauze, de stop of het einde van de avond
- **Begin** en **Klaar**: de kloktijden waartussen het liep
- **Duur**: hoe lang het echt liep, wat iets anders is dan hoe lang het nummer duurt: een dans die na veertig seconden werd afgebroken zegt veertig seconden
- **Status**: afgespeeld, overgeslagen, of een bestand dat weg was toen de avond eraan toe was. Een bestand dat er niet was is geen keuze van iemand, en wordt dus ook niet zo genoteerd

Waar de avond begint en waar hij eindigt staan als regels in de lijst.

### Geschiedeniswerkbalk

- **Naar wachtrij**: terug naar de wachtrijweergave
- **Welke avond**: vanavond, of een avond die is opgeborgen. De rest van deze werkbalk werkt op de avond waar je naar kijkt.
- **Geschiedenis exporteren**: bewaart die avond als JSON. Handig als verslag van een avond.
- **Nieuwe avond**: bewaart deze avond en begint een nieuwe (met bevestiging). Er wordt niets verwijderd: de avond wordt opgeborgen en de geschiedenis begint leeg. Handig na een soundcheck, of op een avond die niet met het eindsignaal is afgesloten.
- **Verwijderen**: gooit die avond weg (met bevestiging). Dit kan niet ongedaan worden gemaakt, en zo blijft het bestand een omvang die iemand gekozen heeft.

### Avonden

Een avond sluit zichzelf af zodra het eindsignaal gespeeld is: de avond wordt bewaard en de geschiedenis begint opnieuw, zodat er tijdens het opruimen niets onthouden hoeft te worden. De avond verdwijnt daarbij niet van het scherm; hij wordt de avond waar je naar kijkt in plaats van de avond die loopt. Elke opgeborgen avond kun je lezen, exporteren en verwijderen, en avonden worden nooit door elkaar gehaald.

Is een avond nooit afgesloten, omdat de applicatie afgesloten werd of de laptop leeg raakte, dan staat hij er bij de volgende start nog. Na meer dan acht uur stilte wordt er een keer gevraagd, voordat er iets speelt, of de avond bewaard moet worden om opnieuw te beginnen of dat ermee doorgegaan wordt. Geen van beide antwoorden verwijdert iets. Opnieuw beginnen bergt de avond op op het moment dat de muziek stopte, niet op het moment dat de vraag gesteld werd, zodat een avond die om twee uur ’s nachts eindigde er ook zo uitziet, hoe lang de laptop daarna ook dicht bleef.

### Statusbalk

Onderaan het geschiedenispaneel:

- **Welke avond**: de avond op het scherm
- **Aantal items**: het aantal regels, of "Geen geschiedenis"

---

## Nummercatalogus

De catalogus toont je bibliotheek, de nummers die je in Review beantwoord hebt, in een
doorzoekbare, sorteerbare tabel. Wat nog wacht staat hier niet; dat staat in [Review](#review-1).

### Nummers bladeren

De catalogus toont de nummers in een tabel met deze kolommen:

- **Dans**: het danstype
- **Artiest**: de artiest of groep
- **Titel**: de titel
- **Duur**: de lengte als MM:SS

Klik op een kolomkop om erop te sorteren. Klik nog eens om de volgorde om te draaien.

### Zoeken

Filter met het zoekveld in de werkbalk. Het zoekt tegelijk in dans, artiest en titel, en de
resultaten volgen je toetsaanslagen. De wisknop maakt het zoekveld leeg.

### Nummers in wachtrij plaatsen

Dubbelklik een nummer om het achteraan de wachtrij te zetten. Staat dubbelpreventie aan in de instellingen, dan kan een nummer dat al speelt, al in de wachtrij staat of al in de geschiedenis zit niet nog eens.

### Een typefout herstellen waar je hem ziet

Rechtsklik een nummer, hier, of op een nummer dat in de wachtrij staat, en kies **Nummer
bewerken** om dans, artiest of titel ter plekke te corrigeren. Het nummer verlaat je bibliotheek
daarbij niet: wat je wijzigt wordt als jouw eigen antwoord opgeslagen, en alleen de velden die je
daadwerkelijk veranderde worden geraakt. De dans moet nog steeds een zijn die de gepubliceerde
lijst kent; een ontbrekende dans is een voorstel bij
[BigBalfolkList](https://tjvl.github.io/BigBalfolkList/), geen lokale uitzondering.

### Schakel naar Danslijst

Met de schakelknop in de werkbalk toont de rechterkolom de danslijst.

---

## Danslijst

De danslijst is elke balfolkdans en elke naam die elke dans draagt. Hij komt van
[BigBalfolkList](https://tjvl.github.io/BigBalfolkList/) en Ready4Balfolk gebruikt hem precies
zoals gepubliceerd: er is niets op te bouwen, niets in te vullen, en niets in het programma
bewerkt hem.

Er wordt geen kopie meegeleverd. De eerste keer dat je het programma opent vraagt de installatie je
om de lijst bij BigBalfolkList op te halen, of om een `dances.json` te importeren die iemand op een
stick heeft meegenomen als deze machine nooit online komt. Zolang dat niet gebeurd is valt er niets
over je muziek te beantwoorden, en het programma haalt nooit uit zichzelf iets op: de knop
Bijwerken in het danslijstpaneel is hoe je om een nieuwere vraagt.

### Wat erin staat

- **De namen van een dans zijn gelijken.** Spelling is omstreden en deze lijst kiest geen kant; de
  eerste naam is simpelweg de naam die het programma toont. Een naam hoort bij precies één dans,
  en dat is wat Ready4Balfolk in staat stelt met één dans te antwoorden als hij een naam in een
  bestandsnaam herkent.
- **Al het andere is een tag**: waar een dans vandaan komt, bij welke familie hij hoort, of hij in
  een suite gedanst wordt. Een dans kan Bretons *én* een gavotte *én* deel van een suite zijn
  zonder onder één ervan opgeborgen te worden.
- **Grammatica is geen spelling.** De lijst draagt twee kleine woordenlijsten mee, zodat een
  telwoord als zijn cijfer telt en lijm als *de*, *la*, *the* en *temps* genegeerd wordt bij het
  vergelijken. Dat maakt `Bourrée à 3 temps`, `Bourrée in 3`, `Bourrée à trois temps` en
  `Bourrée 3t` één dans, en laat een bibliotheek in het Frans, Nederlands of Duits überhaupt
  matchen.

### Kiezen waar willekeurig uit getrokken wordt

De tags in de linkerbalk zijn de **pool**. Een klik laat een tag door drie standen lopen: eruit,
**wordt uit getrokken** (gevuld), en **nooit trekken** (rode rand, doorgestreept); een derde klik
zet hem er weer uit. Een willekeurige keuze, en de auto-wachtrij, trekken uit de dansen die een
gekozen tag dragen en geen enkele uitgesloten tag, een uitsluiting wint altijd, dus *bretagne*
maar nooit *chain* betekent precies dat. Zonder keuze is de pool elke dans.

De werkbalk zegt altijd waaruit getrokken wordt, want een tag is snel aangeklikt en daarna moeilijk
op te merken. **Alles** maakt de pool weer leeg.

Tags zijn groter naarmate meer dansen ze dragen, en een tag op een kaart aanklikken doet hetzelfde
als in de balk.

### Eén bepaalde dans

Klik de **dobbelsteen** op een dans om een willekeurig nummer van precies die dans toe te voegen,
wat de pool ook zegt. Een dans waar je geen nummers van hebt zegt dat, en kan ook nooit in een
willekeurige keuze opduiken.

### Zoeken

Het zoekveld matcht elke spelling van elke dans, ongeacht hoofdletters, accenten en leestekens:
`hanterdro` vindt *Hanter dro* en `bourree 3` vindt *Bourrée à trois temps*.

### Bijhouden

- **Bijwerken**: haalt de lijst op zoals BigBalfolkList hem nu publiceert. Handig als je weet dat
  er net iets is toegevoegd.
- **Uit een bestand**: neemt de lijst uit een `dances.json` op deze computer, voor een machine die
  nooit internet ziet.

In beide gevallen wordt de lijst in zijn geheel vervangen en eerst gecontroleerd; is hij
onleesbaar, dan blijft de lijst die je al had in gebruik.

### Iets dat ontbreekt of verkeerd gespeld is?

Stel het voor bij [BigBalfolkList](https://tjvl.github.io/BigBalfolkList/). De site laat je een
spelling toevoegen, er een verbeteren, een dans taggen of een ontbrekende dans toevoegen, en maakt
daar een voorstel van dat iemand bekijkt. Iedereen die de lijst gebruikt krijgt jouw verbetering: dat is het punt van één lijst.

---

## Review

Niets bereikt je bibliotheek voordat het een artiest, een titel, een dans uit de gepubliceerde
lijst en jouw akkoord op alle drie heeft. Review is waar dat akkoord gegeven wordt, en het is een
vast onderdeel van het programma in plaats van een stap van de setup: wijzig tags, hernoem een
bestand of zet er nieuwe bij, en ze komen hier vanzelf terug met wat je eerder antwoordde er nog
op.

Een eerste start toont daarom een bibliotheek zonder muziek en een wachtrij met alles. Dat klopt,
en het is precies de bedoeling: een nummer dat door een gok is ingevuld waar niemand naar keek is
erger dan een dat eerlijk nog wacht.

### Wat de wachtrij toont

- **Eén regel per nummer**, niet één per fout. Ook een bestand dat helemaal niets over zichzelf
  zegt moet te beantwoorden zijn, en dat kan nooit opduiken in een lijst van verkeerd gespelde
  dingen.
- **Het minst zekere eerst**, zodat halverwege stoppen je bibliotheek beter achterlaat in plaats
  van alleen anders. Wie veertig regels beantwoordt, heeft de veertig beantwoord waar niets voor
  kon spreken.
- **Gegroepeerd per map**, met de mapnaam in een band boven zijn nummers. Nummers die los in je
  muziekmap liggen staan apart: ze zijn nergens opgeborgen, dus er is niets om samen te
  beantwoorden.
- **Elk veld zegt waar het vandaan komt**: een tag, de bestandsnaam, een map, een van je regels,
  of jij, want een fout antwoord valt pas op als je ziet wat het opleverde.

### Het toetsenbord

| | |
|---|---|
| Omhoog, Omlaag | tussen nummers |
| Tab | tussen de drie velden van een nummer, rondlopend |
| Enter | dit nummer beantwoorden en door naar het volgende dat wacht |
| Shift+Enter | elk volledig nummer in deze map beantwoorden |
| Shift+Spatie | het geselecteerde nummer beluisteren |
| Escape | stoppen met luisteren |
| Links, Rechts | vijf seconden springen, terwijl er iets speelt |

Een nummer selecteren zet de cursor in zijn eerste lege veld, dus je kunt gewoon typen. Bij het
typen van een dans verschijnen de namen die de lijst kent; de pijltjes lopen erdoorheen en Enter
neemt de gemarkeerde.

Een map beantwoorden **bevestigt** in plaats van in te vullen: elk nummer houdt de artiest en titel
die het al heeft, en een nummer dat nog iets mist blijft staan. Een map van meer dan een handvol
vraagt eerst.

Kan een nummer niet beantwoord worden, dan knippert de regel rood in plaats van stilletjes niets te
doen; een map vragen wijst elk nummer aan dat hem tegenhoudt.

### Kleuren

| | |
|---|---|
| gewoon | wacht op jou |
| groen | beantwoord, en in je bibliotheek |
| oranje | beantwoord, en wacht tot de danslijst de naam draagt die jij hem gaf |

### Als een dans niet in de lijst staat

De regel biedt aan wat de naam bedoeld kan hebben, zodat een spelfout één klik is in plaats van
overtypen. Daarnaast:

- **Gebruik voor alle N die X zeggen** zet die dans op elk wachtend nummer met dezelfde claim. Het
  zet de dans en niets anders, dus elk van die nummers wil nog steeds zijn eigen bevestiging:
  artiesten en titels worden niet gedeeld.
- **X is geen dans** zegt dat de waarde troep is. `trad` is geen dans en wordt het nooit, dus hij
  wordt overal gewist waar hij opduikt en onthouden, en een nieuwe scan zet hem niet terug. Die
  nummers hebben nog steeds een echte dans nodig.

Een dans die werkelijk ontbreekt hoort in een voorstel bij
[BigBalfolkList](https://tjvl.github.io/BigBalfolkList/), gelinkt vanuit het Regels-paneel. Je
antwoord blijft intussen bewaard, en zodra een bijgewerkte lijst de naam draagt gaan die nummers je
bibliotheek in zonder dat je iets gevraagd wordt.

---

## Instellingen

Het instellingenscherm regelt het gedrag van het programma. Alles wordt automatisch opgeslagen.

### Muziekmap

Waar je muziek staat, getoond in plaats van bewerkt: hem wijzigen leest de hele bibliotheek
opnieuw en beslist elk nummer opnieuw, en dat is setup in plaats van een instelling om aan te
schuiven. **Setup opnieuw uitvoeren** is de weg. Alles onder die map telt mee, hoe hij ook is
ingedeeld.

### Maximaal aantal wachtrijitems

Het maximum in de wachtrij, tussen 1 en 100. Is de wachtrij vol, dan kan er niets bij tot er iets
gespeeld of verwijderd is.

### Pauzeduur

De standaardduur (in seconden) van pauzemarkeringen, tussen 1 en 300. Dit is wat "Pauze toevoegen"
in de wachtrijwerkbalk gebruikt.

### Geen verzoeken meer na een eindtijd

Een eindtijd voor de avond. Zodra de wachtrij voorbij die tijd zou lopen (plus een respijt in
minuten), worden nieuwe items geweigerd, zo eindigt de laatste dans wanneer de zaal sluit in
plaats van twintig minuten erna. De auto-wachtrij houdt zich aan dezelfde grens en stopt met
aanvullen, in plaats van de avond op eigen houtje door te laten lopen. Staat er een stop in de
wachtrij, dan is de eindtijd onbekend en geldt de grens niet; gebruik een pauze als je weet hoe lang
de onderbreking duurt.

### Slotmuziek van de avond

De muziek die zegt dat het bal voorbij is: stoppen met dansen, jas zoeken, stoelen stapelen. Eén
bestand, waar jij het ook bewaart. Het wordt niet geïmporteerd en komt nooit in de bibliotheek,
want het heeft geen dans, geen artiest en geen titel en zou eeuwig in de reviewwachtrij om die
staan te vragen. Typ het pad of gebruik de bladerknop; laat het leeg en de knop in de
wachtrijwerkbalk blijft uit, net als wanneer het bestand later verhuist.

Het in de wachtrij zetten sluit de avond af. Er gaat daarna niets meer bij, geen nummer, geen
verzoek, geen pauze en geen bericht, en de auto-wachtrij stopt zodat het programma geen avond
verlengt die jij net beëindigd hebt. Haal je het weg, dan gaat de avond weer open.

Staat **speel de slotmuziek van de avond zodra deze tijd bereikt is** aan, dan wordt het voor je in
de wachtrij gezet zodra de eindtijd het volgende nummer weigert: het laatste wat de zaal hoort is dan
het teken om naar huis te gaan, zonder dat iemand er tijdens het opruimen aan hoeft te denken. De
eindtijd weigert de slotmuziek zelf nooit: die hoort ná de grens, hij probeert er niet langs.

### Hoe nummers op het scherm komen te staan

Vier vakjes, een per plek waar het programma een nummer als regel schrijft: de grote regel terwijl er
iets speelt, de regel eronder, een regel van de wachtrij en een regel van de geschiedenis. De
plaatshouders zijn dezelfde als bij de bestandsnaampatronen, maar dan andersom gelezen: `%d` de dans,
`%a` de artiest, `%t` de titel. Al het andere komt er letterlijk te staan zoals je het typt, dus
`%t (%d)` geeft "Salamandre (Mazurka)".

Een veld waar in jouw bestanden niets in staat neemt zijn scheidingsteken mee. `%a - %t` op een
nummer zonder titel is de artiest, niet de artiest met een bungelend streepje, en een nummer dat
niets heeft van wat het sjabloon vraagt levert helemaal niets op in plaats van een regel leestekens.

De voorbeeldregels onder de vakjes laten zien wat de vier met een nummer doen, zodat je niets hoeft
op te slaan om erachter te komen. De wijziging is meteen op elk scherm te zien, ook op een nummer dat
op dat moment speelt.

Het nummeroverzicht houdt zijn kolommen. Dat is een tabel die je sorteert op dans, artiest of titel,
en een kolom is geen zin.

### Tekst op knoppen

Vervangt de iconen op knoppen door een korte omschrijving van wat ze doen. Handig tijdens het leren
kennen van het programma, of voor wie woorden boven pictogrammen verkiest.

### Taal

De taal van het programma, Engels of Nederlands. Werkt na een herstart.

### Presentatieschermen

Het aantal presentatievensters, tussen 0 en 10. Op 0 staan ze uit. Presentatievensters zijn bedoeld
voor projectors of externe schermen die de zaal ziet.

### Automatisch willekeurig nummer

Aangezet staat er onderaan de wachtrij altijd een willekeurig gekozen nummer klaar zolang er iets
speelt, zodat de muziek niet zomaar ophoudt. Het auto-nummer verschijnt vervaagd en kan vernieuwd
(ander nummer) of vastgezet (blijft staan) worden. Het blijft onder alles staan wat je zelf
toevoegt, en er wordt een nieuw nummer gekozen zodra het vorige begint te spelen. Heb je een
eindtijd voor de avond ingesteld, dan stopt het aanvullen zodra een nummer daar voorbij zou lopen.

### Dubbele nummers toestaan

Uitgezet kan een nummer dat al speelt, al in de wachtrij staat of al in de geschiedenis zit niet
nog eens toegevoegd worden. Zo speelt hetzelfde nummer niet twee keer op een avond.

### Afspeelacties bevestigen

Aangezet (de standaard) verschijnt een bevestiging vóór overslaan, wissen, herstarten of springen
naar een plek die je op de voortgangsbalk aanklikt. Zet het uit als de bevestigingen tijdens een
optreden storen.

### Thema

Drie keuzes:

- **Auto**: volgt het systeemthema
- **Licht**: licht kleurenschema
- **Donker**: donker kleurenschema

### Logbestand exporteren

Bewaart het logbestand van het programma. Nuttig bij het uitzoeken van problemen of het melden van
een fout.

---

## Presentatiescherm

Presentatievensters zijn schermvullende weergaven voor projectors of externe monitoren, die de zaal
tonen wat er speelt en wat er komt.

### Indeling

- **Bovenste helft**: de huidige dansnaam in grote letters, met artiest en titel eronder. Speelt er
  een bericht, dan staat de berichttekst er.
- **Voortgangsbalk**: een groene balk in het midden
- **Onderste helft**: de volgende dans en zijn gegevens, of "Geen volgend nummer" bij een lege
  wachtrij
- **Achter een pauze**: staat er een pauze, een stop of een bericht als volgende en zit daar
  meteen een dans achter, dan staat die dans er ook onder. Zulke items zet je juist in de wachtrij
  zodat de zaal rijen kan vormen of een partner kan zoeken, en dan wil de vloer weten waarvoor. Het
  webscherm laat hetzelfde zien.

### Configuratie

Stel het aantal vensters in bij **Instellingen > Presentatieschermen**. Elk venster kan naar een
ander scherm en onthoudt zijn plek tussen sessies.

---

## Telefoonafstandsbediening en webweergave

Ready4Balfolk kan twee pagina's over je lokale netwerk aanbieden vanuit een kleine ingebouwde
webserver:

- **De weergavepagina** toont wat er speelt en wat er komt, voor elk apparaat met een browser: een
  tablet naast het podium werkt zo als presentatiescherm zonder videokabel.
- **De afstandsbediening** kan afspelen, pauzeren, overslaan, een willekeurig nummer, een stop, een
  pauze of een bericht toevoegen, de avond afsluiten, en de bibliotheek doorzoeken, wat een DJ
  nodig heeft weg van de computer, en niets meer. Bewust kan hij de pool niet wijzigen: waar
  willekeurige keuzes uit trekken wordt aan de computer besloten, en de afstandsbediening trekt uit
  wat het scherm daar zegt. De avond afsluiten werkt net zo: welk bestand dat is, is aan de computer
  bepaald, en staat er geen, dan zegt de afstandsbediening dat in plaats van er een te kiezen.

Zolang er iets wordt aangeboden zegt de werkbalk dat, naast de reviewknop: **Scherm** als de
schermpagina draait, **Afstandsbediening** als die er ook is. Ze volgen de server en niet de schakelaar,
dus een poort die al bezet is levert niets op in plaats van een belofte. Klik erop en het adres komt
in beeld als QR-code waar je een telefoon op richt, met het adres eronder en bij de afstandsbediening
de pincode ernaast. Zit de computer op meerdere netwerken, dan staan de andere adressen onder de
code, want maar een daarvan is het netwerk waar de telefoon op zit.

Beide worden aangezet in **Instellingen**. De afstandsbediening staat uit tot jij hem aanzet, en
is beveiligd met een pincode: wie de pagina kan bereiken en de pincode kent, kan veranderen waar de
zaal op danst, dus behandel hem daarnaar. **Nieuwe pincode** maakt een verse en verbreekt elke
telefoon die de oude gebruikt. Die telefoon krijgt dat te zien en komt weer op het pincodeformulier,
zodat een helper aan de bar het verschil merkt tussen buitengesloten zijn en een programma dat
ermee opgehouden is. Hetzelfde gebeurt met een telefoon waarvan de toegang gewoon is verlopen: die
vraagt om de pincode in plaats van een afstandsbediening te tonen die niets doet.

De willekeurige keuze op de afstandsbediening trekt uit dezelfde pool als de computer, uitsluitingen
inbegrepen.
