namespace UnshackledWord.Application.Tests.Features.LinkRoles;

/// <summary>
/// Real verses from uw_0018_test (2026-11-17), tab-separated: <c>W</c> = Elb1871Words + Elb1871Morphology (id, position, text, lemma,
/// STTS, person, number, gender, case), <c>L</c> = mapping row (id, German word, step word, Strong's, added, part), <c>S</c> = source
/// word (id, Greek Morphology / Hebrew Grammar, ExpandedStrongTags). Tests change single rows to build "before" or "after 0018.5" variants.
/// </summary>
internal static class LinkRoleFixtures
{
    public const int Gen1v3Ref = 1001003;

    public const string Gen1v3 = """
        W	31	1	Und	und	KON				
        W	32	2	Gott	Gott	NN		S	M	Nom
        W	33	3	sprach	sprechen	VVFIN	3	S		
        W	34	4	Es	es	PPER	3	S	N	Nom
        W	35	5	werde	werden	VAFIN	3	S		
        W	36	6	Licht	Licht	NN		S	N	Nom
        W	37	7	und	und	KON				
        W	38	8	es	es	PPER	3	S	N	Nom
        W	39	9	ward	werden	VAFIN	3	S		
        W	40	10	Licht	Licht	NN		S	N	Nom
        L	31	31	22			
        L	32	32	23			
        L	33	33	22			
        L	34	34			1	
        L	35	35	24			
        L	36	36	25			
        L	37	37	26			
        L	38	38			1	
        L	39	39	26			
        L	40	40	27			
        S	22	Hc/Vqw3ms	H9001=ו=&/{H0559=אָמַר=to say}
        S	23	HNcmpa	{H0430G=אֱלֹהִים=God»LORD@Gen.1.1-Heb}
        S	24	HVqj3ms	{H1961=הָיָה=to be}
        S	25	HNcfsa	{H0216=אוֹר=light}
        S	26	Hc/Vqw3ms	H9001=ו=&/{H1961=הָיָה=to be}\H9014=־=link
        S	27	HNcfsa	{H0216=אוֹר=light}\H9016=׃=verseEnd
        """;

    public const int Mk1v8Ref = 41001008;

    public const string Mk1v8 = """
        W	574858	1	Ich	ich	PPER	1	S		Nom
        W	574859	2	zwar	zwar	ADV				
        W	574860	3	habe	haben	VAFIN	3	S		
        W	574861	4	euch	euch	PPER	2	P		Dat
        W	574862	5	mit	mit	APPR				
        W	574863	6	Wasser	Wasser	NN		S	N	Nom
        W	574864	7	getauft	taufen	VVPP				
        W	574865	8	er	er	PPER	3	S	M	Nom
        W	574866	9	aber	aber	KON				
        W	574867	10	wird	werden	VAFIN	3	S		
        W	574868	11	euch	euch	PPER	2	P		Dat
        W	574869	12	mit	mit	APPR				
        W	574870	13	Heiligem	heilig	ADJA		S	N	Nom
        W	574871	14	Geiste	Geist	NN		P	M	Nom
        W	574872	15	taufen	taufen	VVINF				
        L	173917	574858	19023	G1473		
        L	173918	574859	19024	G3303		
        L	173919	574860	19025	G0907		
        L	173920	574861	19026	G4771		
        L	173921	574862	19027	G1722		
        L	173922	574863	19028	G5204		
        L	173923	574864	19025	G0907		
        L	173924	574865	19029	G0846		
        L	173925	574866	19030	G1161		
        L	173926	574867	19031	G0907		
        L	173927	574868	19032	G4771		
        L	173928	574869	19033	G1722		
        L	173929	574870	19035	G0040G		
        L	173930	574871	19034	G4151G		
        L	173931	574872	19031	G0907		
        S	19023	P-1NS	
        S	19024	PRT	
        S	19025	V-AAI-1S	
        S	19026	P-2AP	
        S	19027	PREP	
        S	19028	N-DSN	
        S	19029	P-NSM	
        S	19030	CONJ	
        S	19031	V-FAI-3S	
        S	19032	P-2AP	
        S	19033	PREP	
        S	19034	N-DSN	
        S	19035	A-DSN	
        """;

    public const int Mk1v29Ref = 41001029;

    public const string Mk1v29 = """
        W	575268	1	Und	und	KON				
        W	575269	2	alsbald	alsbald	ADV				
        W	575270	3	gingen	gehen	VVFIN	3	P		
        W	575271	4	sie	sie	PPER	3	P		Nom
        W	575272	5	aus	aus	PTKVZ				
        W	575273	6	der	der	ART		S	M	Nom
        W	575274	7	Synagoge	Synagoge	NN		S	F	Nom
        W	575275	8	und	und	KON				
        W	575276	9	kamen	kommen	VVFIN	3	P		
        W	575277	10	in	in	APPR				
        W	575278	11	das	der	ART		S	N	Nom
        W	575279	12	Haus	Haus	NN		S	N	Nom
        W	575280	13	Simons	Simons	NE		S		Nom
        W	575281	14	und	und	KON				
        W	575282	15	Andreas	Andreas	NE		S	M	Nom
        W	575283	16	mit	mit	APPR				
        W	575284	17	Jakobus	Jakobus	NE		S	M	Nom
        W	575285	18	und	und	KON				
        W	575286	19	Johannes	Johannes	NE		S	M	Nom
        L	22830	575268	19377	G2532		
        L	22831	575269	19378	G2112		
        L	22832	575270	19382	G1831		
        L	22833	575271			1	
        L	22834	575272	19379	G1537		
        L	22835	575273	19380	G3588		
        L	22836	575274	19381	G4864		
        L	22837	575275			1	
        L	22838	575276	19383	G2064		
        L	22839	575277	19384	G1519		
        L	22840	575278	19385	G3588		
        L	22841	575279	19386	G3614G		
        L	22842	575280	19387	G4613O		
        L	22843	575281	19388	G2532		
        L	22844	575282	19389	G0406		
        L	22845	575283	19390	G3326		
        L	22846	575284	19391	G2385G		
        L	22847	575285	19392	G2532		
        L	22848	575286	19393	G2491H		
        S	19377	CONJ	
        S	19378	ADV	
        S	19379	PREP	
        S	19380	T-GSF	
        S	19381	N-GSF	
        S	19382	V-2AAP-NPM	
        S	19383	V-2AAI-3P	
        S	19384	PREP	
        S	19385	T-ASF	
        S	19386	N-ASF	
        S	19387	N-GSM-P	
        S	19388	CONJ	
        S	19389	N-GSM-P	
        S	19390	PREP	
        S	19391	N-GSM-P	
        S	19392	CONJ	
        S	19393	N-GSM-P	
        """;

    public const int Mk1v35Ref = 41001035;

    public const string Mk1v35 = """
        W	575380	1	Und	und	KON				
        W	575381	2	frühmorgens	Frühmorgens	NE		S	N	Gen
        W	575382	3	als	als	KOKOM				
        W	575383	4	es	es	PPER	3	S	N	Nom
        W	575384	5	noch	noch	KON				
        W	575385	6	sehr	sehr	ADV				
        W	575386	7	dunkel	dunkel	ADJD				
        W	575387	8	war	sein	VAFIN	3	S		
        W	575388	9	stand	stehen	VVFIN	3	S		
        W	575389	10	er	er	PPER	3	S	M	Nom
        W	575390	11	auf	auf	PTKVZ				
        W	575391	12	und	und	KON				
        W	575392	13	ging	gehen	VVFIN	3	S		
        W	575393	14	hinaus	hinaus	APZR				
        W	575394	15	und	und	KON				
        W	575395	16	ging	gehen	VVFIN	3	S		
        W	575396	17	hin	hin	ADV				
        W	575397	18	an	an	PTKVZ				
        W	575398	19	einen	ein	ART		S	M	Acc
        W	575399	20	öden	öde	ADJA		S	M	Acc
        W	575400	21	Ort	Ort	NN		S	M	Nom
        W	575401	22	und	und	KON				
        W	575402	23	betete	beten	VVFIN	3	S		
        W	575403	24	daselbst	daselbst	PROAV				
        L	22942	575380	19471	G2532		
        L	22943	575381	19472	G4404		
        L	22944	575382			1	
        L	22945	575383			1	
        L	22946	575384	19473	G1773		
        L	22947	575385	19474	G3029		
        L	22948	575386	19473	G1773		
        L	22949	575387			1	
        L	22950	575388	19475	G0450		
        L	22951	575389			1	
        L	22952	575390	19475	G0450		
        L	22953	575391			1	
        L	22954	575392	19476	G1831		
        L	22955	575393	19476	G1831		
        L	22956	575394	19477	G2532		
        L	22957	575395	19478	G0565		
        L	22958	575396	19478	G0565		
        L	22959	575397	19479	G1519		
        L	22960	575398			1	
        L	22961	575399	19480	G2048		
        L	22962	575400	19481	G5117		
        L	22963	575401	19482	G2532		
        L	22964	575402	19483	G4336		
        L	22965	575403	19482	G2532		
        S	19471	CONJ	
        S	19472	ADV	
        S	19473	ADV	
        S	19474	ADV	
        S	19475	V-2AAP-NSM	
        S	19476	V-2AAI-3S	
        S	19477	CONJ	
        S	19478	V-2AAI-3S	
        S	19479	PREP	
        S	19480	A-ASM	
        S	19481	N-ASM	
        S	19482	CONJ + G1563	
        S	19483	V-INI-3S	
        """;

    public const int Rt1v5Ref = 8001005;

    public const string Rt1v5 = """
        W	176163	1	Da	da	KOUS				
        W	176164	2	starben	sterben	VVFIN	3	P		
        W	176165	3	auch	auch	ADV				
        W	176166	4	die	der	ART		S	F	Nom
        W	176167	5	beiden	beide	PIAT		P		Nom
        W	176168	6	Machlon	Machlon	NE		S	M	Nom
        W	176169	7	und	und	KON				
        W	176170	8	Kiljon	Kiljon	NE		S		Nom
        W	176171	9	und	und	KON				
        W	176172	10	das	der	ART		S	N	Nom
        W	176173	11	Weib	Weib	NN		S	N	Nom
        W	176174	12	blieb	bleiben	VVFIN	3	S		
        W	176175	13	allein	allein	ADV				
        W	176176	14	übrig	übrig	PTKVZ				
        W	176177	15	von	von	APPR				
        W	176178	16	ihren	ihr	PPOSAT		S	M	Acc
        W	176179	17	beiden	beide	PIAT		P		Nom
        W	176180	18	Söhnen	Söhn	NN		P	M	Dat
        W	176181	19	und	und	KON				
        W	176182	20	von	von	APPR				
        W	176183	21	ihrem	ihr	PPOSAT		S	N	Dat
        W	176184	22	Manne	Manne	NN		S	M	Nom
        L	390615	176163	100004			
        L	390616	176164	100004			
        L	390617	176165	100005			
        L	390618	176166	100006		1	
        L	390619	176167	100006			
        L	390620	176168	100007			
        L	390621	176169	100008			
        L	390622	176170	100008			
        L	390623	176171	100009			
        L	390624	176172	100010		1	
        L	390625	176173	100010			
        L	390626	176174	100009			
        L	390627	176175	100009			
        L	390628	176176	100009			
        L	390629	176177	100011			
        L	390630	176178	100012			
        L	390631	176179	100011			
        L	390632	176180	100012			
        L	390633	176181	100013			
        L	390634	176182	100013			
        L	390635	176183	100013			
        L	390636	176184	100013			
        S	100004	Hc/Vqw3mp	H9001=ו=&/{H4191=מוּת=to die}
        S	100005	HD	{H1571=גַּם=also}\H9014=־=link
        S	100006	HNcbdc/Sp3mp	{H8147=שְׁנַ֫יִם=two}/H9028=Pp3m=their
        S	100007	HNpm	{H4248=מַחְלוֹן=Mahlon»Mahlon@Rut.1.2-}
        S	100008	HC/Npm	H9002=ו=and/{H3630=כִּלְיוֹן=Chilion»Chilion@Rut.1.2-}
        S	100009	Hc/VNw3fs	H9001=ו=&/{H7604=שָׁאַר=to remain}
        S	100010	HTd/Ncfsa	H9009=ה=the/{H0802G=אִשָּׁה=: woman»woman:1_woman}
        S	100011	HR/Ncbdc	H9006=מ=from/{H8147=שְׁנַ֫יִם=two}
        S	100012	HNcmpc/Sp3fs	{H3206=יֶ֫לֶד=youth}/H9024=Ps3f=her
        S	100013	HC/R/Ncmsc/Sp3fs	H9002=ו=and/H9006=מ=from/{H0376H=אִישׁ=: husband»man:2_husband}/H9024=Ps3f=her\H9016=׃=verseEnd
        """;

    public const int Rt1v22Ref = 8001022;

    public const string Rt1v22 = """
        W	176623	1	Und	und	KON				
        W	176624	2	so	so	ADV				
        W	176625	3	kehrte	kehren	VVFIN	3	S		
        W	176626	4	Noomi	Noomi	NE		S	F	Nom
        W	176627	5	zurück	zurück	PTKVZ				
        W	176628	6	und	und	KON				
        W	176629	7	Ruth	Ruth	NE		S	F	Nom
        W	176630	8	die	der	ART		S	F	Nom
        W	176631	9	Moabitin	Moabitin	NN		S	F	Nom
        W	176632	10	ihre	ihr	PPOSAT		S	F	Nom
        W	176633	11	Schwiegertochter	Schwiegertochter	NN		S	F	Nom
        W	176634	12	mit	mit	APPR				
        W	176635	13	ihr	ihr	PPOSAT	3	S	F	Dat
        W	176636	14	welche	welcher	PWAT		S	F	Nom
        W	176637	15	aus	aus	PTKVZ				
        W	176638	16	den	der	ART		S	M	Acc
        W	176639	17	Gefilden	Gefilde	NN			M	Nom
        W	176640	18	Moabs	Moabs	NE		S		Nom
        W	176641	19	zurückkehrte	zurückkehren	VVFIN	3	S		
        W	176642	20	und	und	KON				
        W	176643	21	sie	sie	PPER	3	P		Nom
        W	176644	22	kamen	kommen	VVFIN	3	P		
        W	176645	23	nach	nach	APPR				
        W	176646	24	Bethlehem	Bethlehem	NE		S	N	Nom
        W	176647	25	beim	bei	APPRART		S	M	Dat
        W	176648	26	Beginn	Beginn	NN		S	M	Nom
        W	176649	27	der	der	ART		S	M	Nom
        W	176650	28	Gerstenernte	Gerstenernt	NN		P	N	Nom
        L	786184	176623	100252			
        L	786185	176624	100258			
        L	786186	176625	100252			
        L	786187	176626	100253			
        L	786188	176627	100252			
        L	786189	176628	100254			
        L	786190	176629	100254			
        L	786191	176630	100255			
        L	786192	176631	100255			
        L	786193	176632	100256			
        L	786194	176633	100256			
        L	786195	176634	100257			
        L	786196	176635	100257			
        L	786197	176636	100258			
        L	786198	176637	100259			
        L	786199	176638	100259			
        L	786200	176639	100259			
        L	786201	176640	100260			
        L	786202	176641	100258			
        L	786203	176642	100261			
        L	786204	176643	100261			
        L	786205	176644	100262			
        L	786206	176645	100263			
        L	786207	176646	100264			
        L	786208	176647	100265			
        L	786209	176648	100265			
        L	786210	176649	100266			
        L	786211	176650	100267			
        S	100252	Hc/Vqw3fs	H9001=ו=&/{H7725G=שׁוּב=: return»to return:1_return}
        S	100253	HNpf	{H5281=נׇעֳמִי=Naomi»Naomi@Rut.1.2-}
        S	100254	HC/Npf	H9002=ו=and/{H7327=רוּת=Ruth»Ruth@Rut.1.4-Mat}
        S	100255	HTd/Ngfsa	H9009=ה=the/{H4125=מוֹאָבִי=Moab»Moab@Gen.19.37b-Zep}
        S	100256	HNcfsc/Sp3fs	{H3618H=כַּלָּה=: daughter-in-law»daughter-in-law:2_daughter-in-law}/H9024=Ps3f=her
        S	100257	HRd/Sp3fs	{H5973A=עִם=with}/H9034=Os3f=her
        S	100258	HTd/Vqp3fs	H9009=ה=the/{H7725G=שׁוּב=: return»to return:1_return}
        S	100259	HR/Ncmpc	H9006=מ=from/{H7704G=שָׂדֶה=: country;_planet»land:2_country;_planet}
        S	100260	HNpl	{H4124G=מוֹאָב=Moab»Moab@Gen.19.37b-Zep}
        S	100261	HC/Pp3mp	H9002=ו=and/{H1992=הֵ֫מָּה=they(masc.)}
        S	100262	HVqp3cp	{H0935G=בּוֹא=: come»to come (in):1_come;_go_in}
        S	100263	HNpl	{H1035G=בֵּית לֶ֫חֶם=Bethlehem»Bethlehem@Gen.35.16-Jhn}+
        S	100264	HNpl	{H1035G=בֵּית לֶ֫חֶם=Bethlehem»Bethlehem@Gen.35.16-Jhn}
        S	100265	HR/Ncfsc	H9003=ב=in/{H8462=תְּחִלָּה=beginning}
        S	100266	HNcmsc	{H7105A=קָצִיר=harvest}
        S	100267	HNcmpa	{H8184=שְׂעֹרָה=barley}\H9016=׃=verseEnd
        """;

    public const int Rt3v4Ref = 8003004;

    public const string Rt3v4 = """
        W	177442	1	Und	und	KON				
        W	177443	2	es	es	PPER	3	S	N	Nom
        W	177444	3	geschehe	geschehen	VVFIN	3	S		
        W	177445	4	wenn	wenn	KOUS				
        W	177446	5	er	er	PPER	3	S	M	Nom
        W	177447	6	sich	sich	PRF	3	S		Acc
        W	177448	7	niederlegt	niederlegen	VVFIN	3	S		
        W	177449	8	so	so	ADV				
        W	177450	9	merke	merken	VVFIN	3	S		
        W	177451	10	den	der	ART		S	M	Acc
        W	177452	11	Ort	Ort	NN		S	M	Nom
        W	177453	12	wo	wo	PWAV				
        W	177454	13	er	er	PPER	3	S	M	Nom
        W	177455	14	sich	sich	PRF	3	S		Acc
        W	177456	15	hinlegt	hinlegen	VVFIN	3	S		
        W	177457	16	und	und	KON				
        W	177458	17	gehe	gehen	VVFIN	3	S		
        W	177459	18	und	und	KON				
        W	177460	19	decke	decken	VVFIN	3	S		
        W	177461	20	auf	auf	PTKVZ				
        W	177462	21	zu	zu	PTKVZ				
        W	177463	22	seinen	sein	PPOSAT		S	M	Acc
        W	177464	23	Füßen	Fuß	NN		P	M	Dat
        W	177465	24	und	und	KON				
        W	177466	25	lege	legen	VVFIN	3	S		
        W	177467	26	dich	dich	PPER	2	S		Acc
        W	177468	27	hin	hin	ADV				
        W	177469	28	er	er	PPER	3	S	M	Nom
        W	177470	29	aber	aber	KON				
        W	177471	30	wird	werden	VAFIN	3	S		
        W	177472	31	dir	dir	PPER	2	S		Dat
        W	177473	32	kundtun	kundtun	VVINF				
        W	177474	33	was	was	PWS		S	N	Nom
        W	177475	34	du	du	PPER	2	S		Nom
        W	177476	35	tun	tun	VVINF				
        W	177477	36	sollst	sollst	VMFIN	3	S		
        L	391295	177442	100687			
        L	391296	177443	100687			
        L	391297	177444	100687			
        L	391298	177445	100688			
        L	391299	177446	100688			
        L	391300	177447	100688			
        L	391301	177448	100688			
        L	391302	177449	100689			
        L	391303	177450	100689			
        L	391304	177451	100690		1	
        L	391305	177452	100691			
        L	391306	177453	100692			
        L	391307	177454	100693			
        L	391308	177455	100693			
        L	391309	177456	100693			
        L	391310	177457	100695			
        L	391311	177458	100695			
        L	391312	177459	100696			
        L	391313	177460	100696			
        L	391314	177461	100697			
        L	391315	177462	100697			
        L	391316	177463	100697			
        L	391317	177464	100697			
        L	391318	177465	100698			
        L	391319	177466	100698			
        L	391320	177467	100698			
        L	391321	177468	100698			
        L	391322	177469	100699			
        L	391323	177470	100699			
        L	391324	177471	100700			
        L	391325	177472	100701			
        L	391326	177473	100700			
        L	391327	177474	100703			
        L	391328	177475	100704			
        L	391329	177476	100704			
        L	391330	177477	100704			
        S	100687	HC/Vqj3ms	H9002=ו=and/{H1961=הָיָה=to be}
        S	100688	HR/Vqcc/Sp3ms	H9003=ב=in/{H7901G=שָׁכַב=: lay_down»to lie down:1_lay_down;_lie_with}/H9043=Ss3m=he
        S	100689	Hc/Vqq2fs	H9001=ו=&/{H3045=יָדַע=to know}
        S	100690	HTo	{H0853=אֵת=[Obj.]}\H9014=־=link
        S	100691	HTd/Ncmsa	H9009=ה=the/{H4725=מָקוֹם=standing place}
        S	100692	HTr	{H0834A=אֲשֶׁר=which}
        S	100693	HVqi3ms	{H7901G=שָׁכַב=: lay_down»to lie down:1_lay_down;_lie_with}\H9014=־=link
        S	100695	Hc/Vqq2fs	H9001=ו=&/{H0935G=בּוֹא=: come»to come (in):1_come;_go_in}
        S	100696	Hc/Vpq2fs	H9001=ו=&/{H1540I=גָּלָה=: uncover[things]»to reveal:3_uncover[things]}
        S	100697	HNcfpc/Sp3ms	{H4772=מַרְגְּלוֹת=feet}/H9023=Ps3m=his
        S	100698	Hc/Vqq2fs	H9001=ו=&/{H7901G=שָׁכַב=: lay_down»to lie down:1_lay_down;_lie_with}
        S	100699	HC/Pp3ms	H9002=ו=and/{H1931=הוּא=he;_she;_it}
        S	100700	HVhi3ms	{H5046=נָגַד=to tell}
        S	100701	HRd/Sp2fs	H9005=ל=to/{H9032=Os2f=you}
        S	100703	HTr	{H0834A=אֲשֶׁר=which}
        S	100704	HVqi2fs/Sn	{H6213A=עָשָׂה=: do»to make:2_do;_perform}/H9013=נ=emph.\H9016=׃=verseEnd
        """;

}
