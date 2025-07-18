﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.CSharp.UnitTests.EmbeddedLanguages.RegularExpressions;

// From https://github.com/dotnet/runtime-assets/blob/main/src/System.Text.RegularExpressions.TestData/Regex_RealWorldPatterns.json
public sealed partial class CSharpRegexParserTests
{
    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/58186")]
    public void TestDeepAlternation()
    {
        // Tree is too large to convert to string and still fit within dll limits.  So we just validate that
        // we were able to parse and get a real tree back.
        var (_, tree, _) = JustParseTree("""
            @"(((http|ftp|https):\/{2})+(([0-9a-z_-]+\.)+(zw|zuerich|zone|zm|zip|zero|zara|zappos|za|yun|yt|youtube|you|yokohama|yoga|yodobashi|ye|yandex|yamaxun|yahoo|yachts|xyz|xxx|xin|xfinity|xerox|xbox|wta|wtc|ws|wow|world|works|work|woodside|wolterskluwer|wme|winners|wine|windows|win|williamhill|wiki|wien|whoswho|wf|weir|wedding|wed|website|weber|webcam|weatherchannel|weather|watches|watch|wang|walter|walmart|wales|vu|voyage|voting|vote|volvo|volkswagen|vodka|vn|vlaanderen|vivo|viva|vision|visa|virgin|vip|vin|villas|viking|vig|video|vi|vg|vet|verisign|ventures|vegas|ve|vc|vanguard|vana|vacations|va|uz|uy|us|ups|uol|university|unicom|uk|ug|ubs|ubank|ua|tz|tw|tvs|tv|tunes|tui|tube|tt|trv|trust|travelersinsurance|travelers|travelchannel|travel|training|trading|trade|tr|tp|toys|toyota|town|tours|total|toshiba|toray|top|tools|tokyo|today|to|tn|tmall|tm|tl|tkmaxx|tk|tjx|tjmaxx|tj|tirol|tires|tips|tiffany|tickets|tiaa|theatre|theater|thd|th|tg|tf|teva|tennis|temasek|tel|technology|tech|team|tdk|td|tci|tc|taxi|tax|tattoo|tatar|tatamotors|target|taobao|talk|taipei|tab|sz|systems|symantec|sydney|sy|sx|swiss|swiftcover|swatch|sv|suzuki|surgery|surf|support|supply|supplies|sun|su|style|study|studio|stream|store|storage|stockholm|stcgroup|stc|statefarm|statebank|star|staples|stada|st|ss|srl|sr|spreadbetting|spot|sport|space|sony|song|solutions|solar|sohu|software|softbank|social|soccer|so|sncf|sn|smile|smart|sm|sling|sl|skype|sky|skin|ski|sk|sj|site|singles|sina|silk|si|shriram|showtime|show|shopping|shop|shoes|shia|shell|shaw|sharp|shangrila|sh|sg|sfr|sexy|sex|sew|seven|ses|services|sener|select|seek|security|secure|seat|search|se|sd|scot|scor|scjohnson|science|schwarz|school|scholarships|schmidt|schaeffler|scb|sca|sc|sbs|sbi|sb|saxo|save|sas|sap|sanofi|sandvikcoromant|sandvik|samsung|samsclub|salon|sale|sakura|safety|safe|saarland|sa|ryukyu|rwe|rw|run|ruhr|rugby|ru|rs|room|rogers|rodeo|rocks|rocher|ro|rmit|rip|rio|ril|rightathome|ricoh|richardli|rich|rexroth|reviews|review|restaurant|rest|republican|report|repair|rentals|rent|ren|reliance|reit|rehab|redumbrella|redstone|red|recipes|realty|realtor|realestate|read|re|raid|radio|racing|qvc|quest|quebec|qpon|qa|py|pwc|pw|pub|pt|ps|prudential|pru|protection|property|properties|promo|progressive|prof|productions|prod|pro|prime|press|praxi|pramerica|pr|post|porn|politie|poker|pohl|pnc|pn|pm|plus|plumbing|playstation|play|place|pl|pk|pizza|pioneer|pink|ping|pin|pid|pictures|pictet|pics|physio|photos|photography|photo|phone|philips|phd|pharmacy|ph|pg|pfizer|pf|pet|pe|pccw|pay|party|parts|partners|pars|paris|panasonic|page|pa|ovh|ott|otsuka|osaka|origins|orientexpress|organic|org|orange|oracle|open|ooo|onyourside|online|onl|ong|one|omega|om|ollo|oldnavy|olayangroup|olayan|okinawa|office|off|observer|obi|nz|nyc|nu|ntt|nrw|nra|nr|np|nowtv|nowruz|now|norton|northwesternmutual|nokia|no|nl|nissay|nissan|ninja|nikon|nike|nico|ni|nhk|ngo|ng|nfl|nf|nexus|nextdirect|next|news|newholland|new|neustar|network|netflix|netbank|net|nec|ne|nc|nba|navy|natura|nationwide|name|nagoya|nab|na|mz|my|mx|mw|mv|mutuelle|mutual|museum|mu|mtr|mtpc|mtn|mt|msd|ms|mr|mq|mp|movie|mov|motorcycles|moto|moscow|mortgage|mormon|monster|money|monash|mom|moi|mobile|mobi|mo|mn|mma|mm|mls|mlb|ml|mk|mitsubishi|mit|mint|mini|mil|microsoft|miami|mh|mg|metlife|merckmsd|menu|men|memorial|meme|melbourne|meet|media|med|me|md|mckinsey|mc|mba|mattel|maserati|marshalls|marriott|markets|marketing|market|map|mango|management|man|makeup|maif|madrid|macys|ma|ly|lv|luxury|luxe|lupin|lundbeck|lu|ltd|lt|ls|lr|lplfinancial|lpl|love|lotto|lotte|london|lol|loft|locus|locker|loans|loan|llp|llc|lk|lixil|living|live|lipsy|link|linde|lincoln|limo|limited|lilly|like|lighting|lifestyle|lifeinsurance|life|lidl|li|lgbt|lexus|lego|legal|lefrak|leclerc|lease|lds|lc|lb|lawyer|law|latrobe|latino|lat|lasalle|lanxess|landrover|land|lancia|lancaster|lamer|lamborghini|lacaixa|la|kz|kyoto|ky|kw|kuokgroup|kred|krd|kr|kpn|kpmg|kp|kosher|komatsu|koeln|kn|km|kiwi|kitchen|kindle|kim|kia|ki|kh|kg|kfh|kerryproperties|kerrylogistics|kerryhotels|ke|kddi|juniper|jprs|jpmorgan|jp|joy|jot|joburg|jobs|jo|jnj|jmp|jm|jll|jio|jewelry|jeep|je|jcp|jcb|java|jaguar|iveco|itv|itau|it|istanbul|ist|ismaili|is|irish|ir|iq|ipiranga|io|investments|intuit|international|intel|int|insure|insurance|institute|ink|ing|info|infiniti|industries|inc|in|imdb|imamat|im|il|ikano|iinet|ifm|ieee|ie|id|icu|ice|icbc|ibm|ÎµÏ…|hyundai|hyatt|hughes|hu|ht|hsbc|hr|how|house|hotmail|hotels|hot|hosting|host|hospital|horse|honda|homesense|homes|homegoods|homedepot|holiday|holdings|hockey|hn|hm|hkt|hk|hiv|hitachi|hisamitsu|hiphop|hgtv|hermes|here|helsinki|help|healthcare|health|hdfcbank|hdfc|hbo|hangout|hamburg|hair|gy|gw|guru|guitars|guide|guge|gucci|guardian|gu|gt|gs|group|grocery|gripe|green|graphics|grainger|gr|gq|gp|gov|got|gop|google|goog|goodyear|goo|golf|goldpoint|gold|godaddy|gn|gmx|gmo|gmail|gm|globo|global|gle|glass|glade|gl|giving|gives|gifts|gift|gi|gh|ggee|gg|gf|george|genting|gent|gea|ge|gdn|gd|gbiz|gb|garage|garden|gap|games|game|gallup|gallo|gallery|gal|ga|fyi|furniture|fund|fun|fujixerox|fujitsu|ftr|frontier|frontdoor|frogans|frl|fresenius|free|fr|fox|foundation|forum|forsale|forex|ford|football|foodnetwork|food|foo|fo|fm|fly|flsmidth|flowers|florist|flir|flights|flickr|fk|fj|fitness|fit|fishing|fish|firmdale|firestone|fire|financial|finance|final|film|fido|fidelity|fiat|fi|ferrero|ferrari|feedback|fedex|fast|fashion|farmers|farm|fans|fan|family|faith|fairwinds|fail|fage|extraspace|express|exposed|expert|exchange|events|eus|eurovision|eu|etisalat|et|esurance|estate|esq|es|erni|ericsson|er|equipment|epson|enterprises|engineering|engineer|energy|emerck|email|eg|ee|education|edu|edeka|eco|ec|eat|earth|dz|dvr|dvag|durban|dupont|dunlop|duck|dubai|dtv|drive|download|dot|doosan|domains|dog|doctor|docs|do|dnp|dm|dk|dj|diy|dish|discover|discount|directory|direct|digital|diet|diamonds|dhl|dev|design|dentist|dental|democrat|delta|deloitte|dell|delivery|degree|deals|dealer|deal|de|dds|dclk|day|datsun|dating|date|data|dance|dad|dabur|cz|cyou|cymru|cy|cx|cw|cv|cuisinella|cu|csc|cruises|cruise|crs|crown|cricket|creditunion|creditcard|credit|cr|cpa|courses|coupons|coupon|count|corsica|coop|cool|cookingchannel|cooking|contractors|contact|consulting|construction|condos|comsec|computer|compare|company|community|commbank|comcast|com|cologne|college|coffee|codes|coach|co|cn|cm|clubmed|club|cloud|clothing|clinique|clinic|click|cleaning|claims|cl|ck|cityeats|city|citic|citi|citadel|cisco|circle|cipriani|ci|church|chrome|christmas|chintai|cheap|chat|chase|charity|channel|chanel|ch|cg|cfd|cfa|cf|cern|ceo|center|ceb|cd|cc|cbs|cbre|cbn|cba|catholic|catering|cat|casino|cash|caseih|case|cars|careers|career|care|cards|caravan|car|captainone|captain|capetown|canon|cancerresearch|camp|camera|cam|calvinklein|call|cal|cab|ca|bzh|bz|by|bw|bv|buzz|buy|business|builders|build|bugatti|budapest|bt|bs|brussels|brother|broker|broadway|bridgestone|bradesco|br|box|boutique|bot|boston|bostik|bosch|booking|book|boo|bond|bofa|boehringer|boats|bo|bnpparibas|bn|bmw|bms|bm|blue|bloomberg|blog|blockbuster|blackfriday|black|bj|biz|bio|bingo|bing|bike|bid|bible|bi|bharti|bh|bg|bf|bet|bestbuy|best|berlin|bentley|beer|beauty|beats|be|bd|bcn|bcg|bbva|bbt|bbc|bb|bayern|bauhaus|basketball|baseball|bargains|barefoot|barclays|barclaycard|barcelona|bar|bank|band|bananarepublic|banamex|baidu|baby|ba|azure|az|axa|ax|aws|aw|avianca|autos|auto|author|auspost|audio|audible|audi|auction|au|attorney|athleta|at|associates|asia|asda|as|arte|art|army|archi|aramco|arab|ar|aquarelle|aq|apple|app|apartments|aol|ao|anz|anquan|android|analytics|an|amsterdam|amica|amfam|amex|americanfamily|americanexpress|am|alstom|alsace|ally|allstate|allfinanz|alipay|alibaba|alfaromeo|al|akdn|airtel|airforce|airbus|aigo|aig|ai|agency|agakhan|ag|africa|afl|afamilycompany|af|aetna|aero|aeg|ae|adult|ads|adac|ad|actor|aco|accountants|accountant|accenture|academy|ac|abudhabi|able|abc|abbvie|abbott|abb|abarth|aarp|aaa)(:[0-9]+)?((\/([~0-9a-zA-Z\#\+\%@\.\/_-]+))?(\?[0-9a-zA-Z\+\%@\/&\[\];=_-]+)?)?))"
            """,
            RegexOptions.None, conversionFailureOk: false);
        Assert.NotNull(tree);
        Assert.Empty(tree.Diagnostics);
    }

    [Theory, MemberData(nameof(GetRealWorldCases))]
    public void TestRealWorldCases(string pattern, int options)
    {
        var (_, tree, _) = JustParseTree($"""
            @"{pattern}"
            """, (RegexOptions)options, conversionFailureOk: false);
        Assert.NotNull(tree);
        Assert.Empty(tree.Diagnostics);
    }

    public static IEnumerable<object[]> GetRealWorldCases()
    {
        using var stream = typeof(CSharpRegexParserTests).Assembly.GetManifestResourceStream("Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.EmbeddedLanguages.RegularExpressions.Regex_RealWorldPatterns.json");
        using var streamReader = new StreamReader(stream!);
        using var textReader = new JsonTextReader(streamReader);

        foreach (var obj in JArray.Load(textReader))
        {
            var options = obj.Value<int>("Options");
            var pattern = obj.Value<string>("Pattern")!.Replace("""
                "
                """, """
                ""
                """);
            yield return new object[] { pattern, options };
        }
    }
}
