using System.Text;
using Microsoft.Extensions.Logging;
using NSubstitute;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Abstractions.Step;
using UnshackledWord.Infrastructure.Services;
using UnshackledWord.Tooling.SeedDb.Services.StepBible.LexiconParser;

namespace UnshackledWord.Tooling.SeedDb.TestRunner.Services.StepBible;

public class StepPersonPlaceLexiconStrategyTests
{
    private readonly StepPersonPlaceLexiconStrategy _sut;
    private readonly IFileService _fileService;
    private readonly IStepPersonPlaceRepository _repo;
    private ILogger<StepPersonPlaceLexiconStrategy> _logger;

    public StepPersonPlaceLexiconStrategyTests()
    {
        _repo = Substitute.For<IStepPersonPlaceRepository>();
        _repo.CountPersonsByFilterAsync(new(),  Arg.Any<CancellationToken>()).Returns(Task.FromResult(0));
        _fileService = Substitute.For<IFileService>();
        _logger = Substitute.For<ILogger<StepPersonPlaceLexiconStrategy>>();
        _sut = new StepPersonPlaceLexiconStrategy(_fileService, _repo, _logger);
    }

    [Theory]
    [MemberData(nameof(GetTestEntries))]
    public async Task ParsePersonPlaceEntries(string fileContent)
    {
        _fileService.ReadAllLinesAsync(Arg.Any<string>(), Arg.Any<Encoding>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(fileContent.Split('\n')));
        await _sut.SaveToDatabase(null!, CancellationToken.None);
    }

    [Fact]
    public async Task ParseLexiconFromFile()
    {
        var fileService = new FileService();
        var service = new StepPersonPlaceLexiconStrategy(fileService, _repo, _logger);

        await service.SaveToDatabase("X:\\Code\\repos\\unshackeled-word\\temp\\SeedData\\Step\\TIPNR - Translators Individualised Proper Names with all References - STEPBible.org CC BY.txt", CancellationToken.None);
    }

    public static IEnumerable<object[]> GetTestEntries()
    {
        yield return new object[]
        {
            """
            ========== PERSON(s)
            Unnamed#1@1Ki.2.27=H0000	Priest living at the time of Conquest	Ithamar@Exo.6.23-Ezr + 			Unnamed#2@1Ki.2.27	Tribe of Levi	#A priest from the tribe of Levi living at the time of Egypt and Wilderness, inferred from 1Ki.2.27; <br>a descendant of  <strong="H0385">Ithamar</strong> (אִיתָמָר), a son of <strong="H0175">Aaron</strong>; <br>an ancestor of <strong="H5941">Eli</strong>.	Male
            – Mentioned	Unnamed#1@1Ki.2.27	H0000=.	[ ]	https://www.stepbible.org/?q=version=ESV|version=KJV|text=Ithamar*|reference=1Ki.2.27	1Ki.2.27
            – Total	Unnamed#1	H0000	1Ki.2.27	1
            @Briefest= A priest
            @Brief= A descendant of Ithamar the son of Aaron.
            @Short= This unnamed descendant of Ithamar was an ancestor of Eli who acted as High Priest.
            @Article= Ithamar was the ancestor of Eli who acted as High Priest (<xref ref='1Sa.14.3'>1Sa.14.3</xref>; <xref ref='1Ki.2.27'>1Ki.2.27</xref>) before Samuel become a Judge. ¶Ithamar's descendants continued to serve as priests (<xref ref='1Ch.24.1-6'>1Ch.24.1-6</xref>). They were divided into divisions for their appointed duties, just as Aaron's other descendants were.. ¶In <xref ref='Ezr.8.2'>Ezr.8.2</xref>, a descendant of Ithamar named Daniel is mentioned among the priests who returned to Jerusalem from the Babylonian exile. This highlights the continuity of Ithamar's lineage in the priesthood.

            """
        };

        yield return new object[]
        {
            """
            ========== PERSON(s)
            Augustus@Luk.2.1-Act=G0828	Emperor living at the time of the New Testament	 + 				Italy	#An emperor of Rome living at the time of the New Testament, first mentioned at Luk.2.1; <br>referred to as <strong="G0828">Augustus</strong> (Αὔγουστος), or <strong="G4575">Augustan</strong> (KJV= Augustus, NIV= Imperial) or "emperor" (KJV= Augustus) (σεβαστός), or <strong="G2541G">Caesar</strong> (Καῖσαρ).	Male
            – Named	Augustus@Luk.2.1-Act	G0828«G0828=Αὔγουστος	Augustus	https://www.stepbible.org/?q=version=ESV|version=KJV|text=Augustus*|reference=Luk.2.1	Luk.2.1
            – Spelled	Augustus@Luk.2.1-Act	G4575«G4575=σεβαστός	Augustan (KJV= Augustus; NIV= Imperial)	https://www.stepbible.org/?q=version=ESV|version=KJV|text=Augustan*|reference=Act.27.1	Act.27.1
            – (same form as previous)	emperor|Augustus@Luk.2.1-Act	G4575«G4575=σεβαστός	emperor (KJV= Augustus)	https://www.stepbible.org/?q=version=ESV|version=KJV|text="emperor"*|reference=Act.25.21;Act.25.25	Act.25.21; Act.25.25
            – Named	Caesar|Augustus@Luk.2.1-Act	G2541G«G2541=Καῖσαρ	Caesar	https://www.stepbible.org/?q=version=ESV|version=KJV|text=Caesar*|reference=Luk.2.1;Luk.3.1	Luk.2.1; Luk.3.1
            – Total	Caesar Augustus	G0828, G4575, G2541G	Luk.2.1; Act.27.1; 25.21,25; Luk.3.1	5
            @Briefest= Roman emperor
            @Brief= Roman emperor during the birth of Jesus
            @Short= Augustus, also known as Caesar, was the Roman emperor during the time of Jesus' birth and early life (Luk.2.1; 3:1; Act.25.21, 25; 27:1).
            @Article= Augustus, also referred to as Caesar, was the first Roman emperor and ruled during the time of Jesus' birth and early life. He is mentioned in the Gospel of Luke and the book of Acts. In <xref ref='Luk.2.1'>Luk.2.1</xref>, a decree is issued by Caesar Augustus that all the world should be registered, leading Joseph and Mary to travel to Bethlehem, where Jesus was born. <xref ref='Luk.3.1'>Luk.3.1</xref> mentions that John the Baptist began his ministry in the fifteenth year of the reign of Tiberius Caesar, indicating that Augustus had died by this time. In Acts, Paul appeals to Caesar as a Roman citizen, referring to the emperor's authority. Augustus' reign was marked by a period of relative peace and stability in the Roman Empire, known as the Pax Romana. His rule had a significant impact on the political and social context of the New Testament world.

            """
        };

        yield return new object[]
        {
            """
            ========== PERSON(s)
            Aaron@Exo.4.14-Heb=H0175	High Priest living at the time of Egypt and Wilderness	Amram@Exo.6.18-1Ch + Jochebed@Exo.6.20-Num	Moses@Exo.2.10-Rev, Miriam@Exo.15.20-Mic	Elisheba@Exo.6.23	Nadab@Exo.6.23-1Ch, Abihu@Exo.6.23-1Ch, Ithamar@Exo.6.23-Ezr, Eleazar@Exo.6.23-Ezr	Tribe of Levi	#A high priest from the tribe of Levi living at the time of Egypt and Wilderness, first mentioned at Exo.4.14; <br>referred to as <strong=\"H0175\">Aaron</strong> (אַהֲרֹן), or <strong=\"G0002\">Aaron</strong> (Ἀαρών); <br> son of <strong=\"H6019G\">Amram</strong> and <strong=\"H3115\">Jochebed</strong>; <br>a brother of <strong=\"H4872\">Moses</strong> and <strong=\"H4813G\">Miriam</strong>; <br>husband of <strong=\"H0472\">Elisheba</strong>; <br> father of <strong=\"H5070G\">Nadab</strong>, <strong=\"H0030\">Abihu</strong>, <strong=\"H0499G\">Eleazar</strong> and <strong=\"H0385\">Ithamar</strong>.	Male
            – Named	Aaron@Exo.4.14-Heb	H0175«H0175=אַהֲרֹן	Aaron	https://www.stepbible.org/?q=version=ESV|version=KJV|text=Aaron*|reference=Exo.4.14;Exo.4.27;Exo.4.28;Exo.4.29;Exo.4.30;Exo.5.1;E
            – Greek	Aaron@Exo.4.14-Heb	G0002«G0002=Ἀαρών	Aaron	https://www.stepbible.org/?q=version=ESV|version=KJV|text=Aaron*|reference=Luk.1.5;Act.7.40;Heb.5.4;Heb.7.11;Heb.9.4	Luk.1.5; Act.7.40; Heb.5.4; Heb.7.11; Heb.9.4
            – Total	Aaron	H0175, G0002	Exo.4.14; Exo.4.27,28,29,30; 5.1,4,20; 6.13,20,23,25,26,27; 7; 8.5,6,8,12,16,17,25; 9.8,27; 10.3,8,16; 11.10; 12.1,28,31,43,50; 15.20; 16.2,6,9,10,33,34; 17.10,12; 18.12; 19.24
            @Briefest= High Priest
            @Brief= Moses' brother, first high priest of Israel
            @Short= Aaron was Moses' older brother and served as the first high priest of Israel.
            @Article= Aaron was the older brother of Moses and Miriam, from the tribe of Levi. God appointed Aaron to be Moses' spokesman when Moses was called to lead the Israelites out of Egypt. Aaron performed signs before Pharaoh, including turning his staff into a snake. He assisted Moses during the Exodus, including holding up Moses' arms during the battle against the Amalekites. ¶God chose Aaron and his descendants to serve as priests for the Israelites. Aaron was consecrated as the first high priest and wore special priestly garments. He was responsible for offering sacrifices, burning incense, and performing other priestly duties in the tabernacle. Aaron also played a role in several significant events, such as the golden calf incident and the rebellion of Korah. ¶Aaron married Elisheba, and they had four sons: Nadab, Abihu, Eleazar, and Ithamar. Nadab and Abihu died when they offered unauthorized fire before the Lord. Aaron died at the age of 123 on Mount Hor, and his son Eleazar succeeded him as high priest. Despite his flaws, Aaron was a key figure in Israel's history and played a crucial role in establishing the priesthood.

            """
        };

        yield return new object[]
        {
            """
            $========== PLACE
            Egypt@Gen.12.10-Rev=H4714G	Egypt	Egypt@Gen.10.6-1Ch		https://www.google.com/maps/@30.108086,31.338220,14z	https://palopenmaps.org/view/9999/@30.108086,31.338220	>	#A location first mentioned at Gen.12.10; <br>referred to as <strong="H4714G">Egypt or Egyptian</strong> (מִצְרַ֫יִם), or <strong="H4693">Egypt</strong> (מָצוֹר), or <strong="H4805G">Egypt</strong> (KJV, NIV= "rebellion")  (מְרִי), or <strong="H7293">Rahab</strong> (רַ֫הַב), or <strong="H4713">Egyptian or Egypt or Egyptian women</strong> (KJV= Egyptians)  (מִצְרִי), or <strong="H2526H">Ham</strong> (חָם), or <strong="G0125">Egypt</strong> (Αἴγυπτος), or <strong="G0124">Egyptian</strong> (Αἰγύπτιος). 	Place
            – Named	Egypt@Gen.12.10-Rev	H4714G«H4714=מִצְרַ֫יִם	Egypt	https://www.stepbible.org/?q=version=ESV|version=KJV|text=Egypt*|reference=Gen.12.10;Gen.12.11;Gen.12.14;Gen.13.1;Gen.13.10;Gen.15.18;Gen.21.21;Gen.25.18;Gen.26.2;Gen.37.25;Gen.37.28;Gen.37.36;Gen.39.1;Gen.40.1;Gen.40.1;Gen.40.5;Gen.41.8
            – (same form as previous)	Egypt@Gen.12.10-Rev	H4714G«H4714=מִצְרַ֫יִם	Egyptian	https://www.stepbible.org/?q=version=ESV|version=KJV|text=Egyptian*|reference=Gen.41.56;Gen.50.11;Exo.3.8;Exo.6.5;Exo.6.6;Exo.6.7;Exo.7.5;Exo.7.21;Exo.8.26;Exo.8.26;
            – Spelled	Egypt@Gen.12.10-Rev	H4693«H4693=מָצוֹר	Egypt	https://www.stepbible.org/?q=version=ESV|version=KJV|text=Egypt*|reference=2Ki.19.24;Isa.19.6;Isa.37.25;Mic.7.12;Mic.7.12	2Ki.19.24; Isa.19.6; Isa.37.25; Mic.7.12a; Mic.7.12b
            – Spelled	Egypt@Gen.12.10-Rev	H4805G«H4805=מְרִי	Egypt (KJV, NIV= rebellion)	https://www.stepbible.org/?q=version=ESV|version=KJV|text=Egypt*|reference=Neh.9.17	Neh.9.17
            – Named	Rahab|Egypt@Gen.12.10-Rev	H7293«H7293=רַ֫הַב	Rahab	https://www.stepbible.org/?q=version=ESV|version=KJV|text=Rahab*|reference=Psa.87.4;Isa.30.7	Psa.87.4; Isa.30.7
            – Group	Egypt@Gen.12.10-Rev	H4713«H4713=מִצְרִי	Egyptian	https://www.stepbible.org/?q=version=ESV|version=KJV|text=Egyptian*|reference=Gen.12.12;Gen.12.14;Gen.16.1;Gen.16.3;Gen.21.9;Gen.25.12;Gen.39.1;Gen.39.2;Gen.39.5;Gen.41.55;Gen.43.32;Gen.43.32;Gen.43.32;Gen.45.2;Gen.46.34;Gen.47.15;Gen.47.20;Gen.50.3;Exo.2.11;Exo.2.12;Exo.2.14;Exo.2.19;Exo.3.9;Exo.3.21;Exo.3.22;Exo.7.18;Exo.7.24;Exo.8.21;Exo.9.6;Exo.9.11;Exo.10.6;Exo.14.4;Exo.14.9;Exo.14.10;Exo.14.12;Exo.14.12;Exo.14.13;Exo.14.17;Exo.14.18;Exo.14.23
            – (same form as previous)	Egypt@Gen.12.10-Rev	H4713«H4713=מִצְרִי	Egypt	https://www.stepbible.org/?q=version=ESV|version=KJV|text=Egypt*|reference=Exo.3.20;Exo.7.5;Exo.7.21;Exo.11.3;1Sa.30.13	Exo.3.20; Exo.7.5; Exo.7.21; Exo.11.3; 1Sa.30.13
            – (same form as previous)	Egypt@Gen.12.10-Rev	H4713«H4713=מִצְרִי	Egyptian women	https://www.stepbible.org/?q=version=ESV|version=KJV|text=Egyptian*|reference=Exo.1.19	Exo.1.19
            – (same form as previous)	Egypt@Gen.12.10-Rev	H4713«H4713=מִצְרִי	they (KJV= Egyptians)	https://www.stepbible.org/?q=version=ESV|version=KJV|text=they*|reference=Exo.1.13	Exo.1.13
            – Named	Ham|Egypt@Gen.12.10-Rev	H2526H«H2526=חָם	Ham	https://www.stepbible.org/?q=version=ESV|version=KJV|text=Ham*|reference=1Ch.4.40;Psa.78.51;Psa.105.23;Psa.105.27;Psa.106.22	1Ch.4.40; Psa.78.51; Psa.105.23; Psa.105.27; Psa.106.22
            – Greek	Egypt@Gen.12.10-Rev	G0125«G0125=Αἴγυπτος	Egypt	https://www.stepbible.org/?q=version=ESV|version=KJV|text=Egypt*|reference=Mat.2.13;Mat.2.14;Mat.2.15;Mat.2.19;Act.2.10;Act.7.9;Act.7.10;Act.7.10;Act.7.11;Act.7.12;Act.7.15;Act.7.17;Act.7.18;Act.7.34;Act.7.34;Act.7.36;Act.7.39;Act.7.40;Act.13.17;Heb.3.16;Heb.8.9;Heb.11.26;Heb.11.27;Jud.1.5;Rev.11.8	Mat.2.13; Mat.2.14; Mat.2.15; Mat.2.19; Act.2.10; Act.7.9; Act.7.10a; Act.7.10b; Act.7.11; Act.7.12; Act.7.15; Act.7.17; Act.7.18; Act.7.34a; Act.7.34b; Act.7.36; Act.7.39; Act.7.40; Act.13.17; Heb.3.16; Heb.8.9; Heb.11.26; Heb.11.27; Jud.1.5; Rev.11.8
            – Group	Egypt@Gen.12.10-Rev	G0124«G0124=Αἰγύπτιος	Egyptian	https://www.stepbible.org/?q=version=ESV|version=KJV|text=Egyptian*|reference=Act.7.22;Act.7.24;Act.7.28;Act.21.38;Heb.11.29	Act.7.22; Act.7.24; Act.7.28; Act.21.38; Heb.11.29
            – Total	Egypt or Rahab or Ham	H4714G, H4693, H4805G, H2526H, G0125, H7293, H4713, G0124	Gen.12.10; ; Gen.12.10 etc.; Exo.1.1 etc.; Lev.11.45 etc.; Num.1.1 etc.; Deu.1.27 etc.; Jos.2.10 etc.; Jdg.2.1 etc.; 1Sa.2.27 etc.; 2Sa.7.6 etc.; 1Ki.3.1 etc.; 2Ki.7.6 etc.; 1Ch.13.5 etc.; 2Ch.1.16 etc.; Neh.9.9 etc.; Psa.68.31 etc.; Isa.7.18 etc.; Jer.2.6 etc.; Lam.5.6; Ezk.16.26 etc.; Dan.9.15 etc.; Hos.2.15 etc.; Jol.3.19; Amo.2.10 etc.; Mic.6.4 etc.; Nam.3.9; Hag.2.5; Zec.10.10 etc.; Gen.50.11; Exo.3.8 etc.; Jdg.10.11; 1Sa.4.8;
            @Briefest=
            @Brief= Ancient nation; Israelites enslaved there; plagues and Exodus
            @Short= Egypt, a powerful nation where the Israelites were enslaved until God delivered them in the Exodus.
            @Article= Egypt was a prominent ancient civilization in northeastern Africa, concentrated along the lower reaches of the Nile River. In the Bible, Egypt is first mentioned as the place Abraham journeyed during a famine in Canaan (Genesis 12:10). Later, Joseph was sold into slavery in Egypt but rose to power as a trusted official of Pharaoh. During a famine, Israel (or 'Jacob') and his family settled in Egypt with Joseph. ¶After generations passed, the Israelites were enslaved by the Egyptians (Exodus 1:8-14). God called Moses to deliver His people from bondage. After Pharaoh refused to free the Israelites, God sent ten plagues upon Egypt (Exodus 7-12). Finally, Pharaoh relented and the Israelites left in the Exodus. At the Red Sea, God miraculously allowed the Israelites to cross on dry ground but destroyed the pursuing Egyptian army (Exodus 14). ¶Egypt is often used in the Bible as a symbol of bondage and oppression. The prophets warned against trusting in Egypt's power instead of God (Isaiah 30:1-3, 31:1). Yet Egypt was also a place of refuge, as when Mary, Joseph and Jesus fled there to escape Herod (Matthew 2:13-15).

            """
        };

        yield return new object[]
        {
            """
            $========== PLACE
            Engedi@Gen.14.7-Ezk=H5872	Engedi			https://www.google.com/maps/@31.46152536164766,35.39241108242345,14z	https://palopenmaps.org/view/9999/@31.46152536164766,35.39241108242345	>	#A location first mentioned at Jos.15.62; <br>referred to as <strong="H5872">Engedi</strong> (עֵין גֶּ֫דִי), or <strong="H2688">Hazazon-tamar</strong> (KJV= Hazezon-tamar, NIV= Hazezon Tamar)  (חַצֲצֹן תָּמָר). 	Place
            – Named	Engedi@Gen.14.7-Ezk	H5872«H5872=עֵין גֶּ֫דִי	Engedi	https://www.stepbible.org/?q=version=ESV|version=KJV|text=Engedi*|reference=Jos.15.62;1Sa.23.29;1Sa.24.1;2Ch.20.2;Sng.1.14;Ezk.47.10	Jos.15.62; 1Sa.23.29; 1Sa.24.1; 2Ch.20.2; Sng.1.14; Ezk.47.10
            – Named	Hazazon-tamar|Engedi@Gen.14.7-Ezk	H2688«H2688=חַצֲצֹן תָּמָר	Hazazon-tamar (KJV= Hazezon-tamar; NIV= Hazezon Tamar)	https://www.stepbible.org/?q=version=ESV|version=KJV|text=Hazazon*|reference=Gen.14.7;2Ch.20.2	Gen.14.7; 2Ch.20.2
            – Total	Engedi or Hazazon-tamar	H5872, H2688	Gen.14.7; Jos.15.62; 1Sa.23.29; 24.1; 2Ch.20.2; Sng.1.14; Ezk.47.10; 2Ch.20.2	8
            @Briefest=
            @Brief= Oasis in Judah; refuge for David; Chedorlaomer's victory
            @Short= Engedi (or Hazazon-tamar), an oasis in Judah where David sought refuge from Saul, also the site of Chedorlaomer's victory.
            @Article= Engedi, also known as Hazazon-tamar, is an oasis located on the western shore of the Dead Sea, in the wilderness of Judah. It is mentioned several times in the Bible, often in the context of its strategic location and its lush vegetation. ¶During the time of Abraham, Engedi (then called Hazazon-tamar) was one of the places where Chedorlaomer, king of Elam, and his allies defeated the Amorites before attacking Sodom and Gomorrah (Genesis 14:7; 2Ch.20.2). ¶Later, when David was fleeing from King Saul, he sought refuge in the strongholds of Engedi (1Sa.23.29). It was in the wilderness of Engedi that David had the opportunity to kill Saul but chose to spare his life, cutting off a corner of Saul's robe instead (1Sa.24.1-7). ¶The Song of Solomon mentions Engedi as a source of fragrant henna blossoms (Sng.1.14), testifying to its fertility and beauty. In Ezekiel's vision of the restored land of Israel, Engedi is mentioned as a place where fishermen will spread their nets, symbolizing abundance and prosperity (Ezk.47.10). ¶The name Engedi means "spring of the kid (young goat)" or "fountain of the goat," likely referring to the freshwater springs that make the area an oasis in the desert. Today, Engedi is a nature reserve in Israel, known for its hiking trails, waterfalls, and diverse flora and fauna.

            """
        };

        yield return new object[]
        {
            """
            $========== OTHER
            Destiny@Isa.65.11=H4507	A male deity in the Old Testament					>	#A male deity in the Old Testament; called <strong="H4507">Destiny</strong> (מְנִי)
            – Named	Destiny@Isa.65.11	H4507«H4507=מְנִי	Destiny (KJV= troop)	https://www.stepbible.org/?q=version=ESV|version=KJV|text=Destiny*|reference=Isa.65.11	Isa.65.11
            – Total	Destiny	H4507	Isa.65.11; 	1
            @Briefest=
            @Brief= A pagan deity mentioned in the Book of Isaiah
            @Short= Destiny, mentioned in Isaiah 65:11, is a pagan deity associated with fate or fortune, which some Israelites worshipped, forsaking the Lord.
            @Article= Destiny is a pagan deity mentioned in the Book of Isaiah, specifically in chapter 65, verse 11. The name "Destiny" is a translation of the Hebrew word "Meni," which is derived from the verb "manah," meaning "to apportion" or "to allot." ¶In the context of Isaiah 65, the Lord is rebuking the Israelites for their idolatry and unfaithfulness. He accuses them of forsaking Him and engaging in pagan practices, such as preparing a table for Gad (another pagan deity associated with fortune) and filling cups of mixed wine for Meni (Destiny). ¶The worship of Destiny likely involved the belief that this deity had the power to control fate and distribute good or bad fortune. By setting a table and filling cups for Destiny, the Israelites were essentially seeking the favor and protection of this false god, rather than trusting in the Lord. ¶The mention of Destiny in Isaiah 65:11 highlights the Israelites' persistent struggle with idolatry and their tendency to adopt the religious practices of the surrounding pagan cultures. The prophet's condemnation of this behavior emphasizes the importance of remaining faithful to the one true God and rejecting the worship of false deities.

            """
        };
    }
}
