using System;
using System.Collections.Generic;
using System.Linq;

namespace EmailValidatorPRO.Services
{
    public class DisposableDomainDetector
    {
        private static readonly HashSet<string> DisposableDomains = new(StringComparer.OrdinalIgnoreCase)
        {
            // --- Temp Mail ---
            "tempmail.com", "guerrillamail.com", "guerrillamailblock.com", "grr.la", "sharklasers.com",
            "guerrillamail.info", "guerrillamail.net", "guerrillamail.org", "spam4.me",
            // --- Mailinator ---
            "mailinator.com", "mailinator2.com", "mailinator.net", "notmailinator.com",
            // --- 10 Minute Mail ---
            "10minutemail.com", "10minutemail.net", "10minutemail.org", "minutemail.com",
            // --- Throwaway ---
            "throwaway.email", "throwawaymail.com", "throwam.com", "thrma.com",
            // --- YOPmail ---
            "yopmail.com", "yopmail.fr", "yopmail.net", "jetable.org", "mailnesia.com",
            // --- TempMail.io ---
            "tempmail.io", "tempmail.ninja", "tempmail.zone",
            // --- Mohmal ---
            "mohmal.com", "mohmal.com.br",
            // --- FakeInbox ---
            "fakeinbox.com", "fakeinbox.co.uk",
            // --- MailDrop ---
            "maildrop.cc", "maildrop.cf",
            // --- Burner ---
            "burnermail.io", "burner-email.com",
            // --- Temp Mail Address ---
            "tempmailaddress.com", "tempmailaddress.net",
            // --- ReceiveSMS ---
            "receivesms.co",
            // --- Trash Mail ---
            "trashmail.com", "trashmail.io", "trash-mail.com", "trashmail.de",
            "trashmail.ws", "spamgourmet.com", "trashymail.com", "mytrashmail.com",
            // --- Guerrilla ---
            "guerrillamail.de", "guerrillamail.biz",
            // --- Harakirimail ---
            "harakirimail.com",
            // --- Mailcatch ---
            "mailcatch.com",
            // --- Dispostable ---
            "dispostable.com",
            // --- FilzMail ---
            "filzmail.com",
            // --- Incognitomail ---
            "incognitomail.org", "incognitomail.net",
            // --- MintEmail ---
            "mintemail.com",
            // --- MyTempEmail ---
            "mytemp.email", "mytempemail.com",
            // --- NullBox ---
            "nullbox.info",
            // --- Shitz ---
            "shitz.email",
            // --- TempMailNow ---
            "tempmailnow.com",
            // --- Tempail ---
            "tempail.com",
            // --- ThrowAwayMail ---
            "throwawaymail.com",
            // --- Regex resolver de subdominios ---
            "tmpmail.net", "tmpmail.org", "tmpmail.com",
            // --- Extra large list ---
            "guerrillamailblock.com", "dispostable.com", "mailcatch.com",
            "mailexpire.com", "mailmoat.com", "mailnull.com", "mailshell.com",
            "mailzilla.com", "meltmail.com", "messagebeamer.de", "mobi.web.id",
            "msa.minsmail.com", "mt2015.com", "mvrht.com", "mx0.wwwnew.eu",
            "nada.email", "nada.life", "nada.ltd", "nedoz.com", "neomailbox.com",
            "nervmich.net", "nexp.x10.mx", "nmail.xyz", "nomail.xl.cx",
            "nomail2.xl.cx", "nospam.ze.tc", "nospam4.us", "notsharingmy.info",
            "nowmymail.com", "nurfuerspam.de", "nus.edu.sg", "nwytg.com",
            "objectmail.com", "obobbo.com", "odaymail.com", "offshorelimits.com",
            "oneoffemail.com", "onewaymail.com", "online.ms", "oopi.org",
            "ordinaryamerican.net", "otherinbox.com", "ourklips.com", "outlawspam.com",
            "ovpn.to", "owlpic.com", "pancakemail.com", "paplease.com",
            "pepbot.com", "pfui.ru", "phonefaker.net", "photomarketing.org",
            "pjjkp.com", "plexolan.de", "politikerclub.de", "poofy.org",
            "popteenblog.com", "postacin.com", "privy-mail.com", "privymail.de",
            "proxymail.eu", "prtnx.com", "pub-mail.net", "punkmail.com",
            "pwrby.com", "putthisinyourspamdatabase.com", "qasti.com", "qisfa.com",
            "qoika.com", "quickemail.com", "quickinbox.com", "ququb.com",
            "rcpt.at", "recode.me", "recursor.net", "recyclemail.dk",
            "regbypass.com", "regbypass.com-safe-mail.net", "rejectmail.com",
            "remail.cf", "remail.ga", "reptilegenetics.com", "riga-mail.com",
            "rhyta.com", "riski.cf", "rppkn.com", "rvlt.cc",
            "s0ny.net", "safersignup.de", "safetymail.info", "safetypost.de",
            "saharanightmail.com", "samsclass.info", "satisfyme.com", "savemailand.de",
            "saynotospams.com", "scbox.one", "scbox.site", "schafmail.de",
            "schneewittchen-muellers.de", "selfdestructingmail.com", "sendfree.org",
            "sendspamhere.com", "sharklasers.com", "shiftmail.com", "shipfromto.com",
            "shitmail.me", "shitmail.org", "shortmail.net", "sibmail.com",
            "sinnlos-mail.de", "siteposter.net", "slapsfromlastnight.com",
            "slopsbox.com", "smashmail.de", "smellfear.com", "snakemail.com",
            "sneakemail.com", "socrazy.net", "sofortmail.de", "solvemail.info",
            "sogetthis.com", "spamavert.com", "spambob.net", "spambob.org",
            "spambox.us", "spamcannon.com", "spamcannon.net", "spamday.com",
            "spamex.com", "spamfighter.org", "spamfree24.org", "spamfree24.com",
            "spamgoes.in", "spamgourmet.com", "spamherelots.com", "spamhole.com",
            "spaminator.de", "spamkill.info", "spaml.de", "spammotel.com",
            "spamobox.com", "spamoff.com", "spamsalad.in", "spamslicer.com",
            "spamthis.co.uk", "spamthisplease.com", "spamtrail.com", "speed.1s.fr",
            "spikio.com", "spoofmail.de", "squizzy.de", "ssoia.com",
            "starlight-breaker.net", "startkeys.com", "statdol.com", "stinkefinger.net",
            "stop-my-spam.com", "storenote.com", "strafpost.de", "stuffmail.de",
            "superstachel.de", "svk.jp", "sweetxxx.de", "swift10minutemail.com",
            "tagyourself.com", "tempail.com", "tempinbox.com", "tempmail.co",
            "tempmail.com", "tempmail.de", "tempmail.eu", "tempmail.lt",
            "tempmail.nu", "tempmail.org", "tempmail.pp.ua", "tempmail.us",
            "tempmail2.com", "tempmailo.com", "tempmailer.com", "tempmailer.de",
            "tempmails.com", "tempoo.us", "tempsky.com", "tempthe.net",
            "thanksnospam.info", "thatim.info", "thc.st", "thecloudindex.com",
            "thelimestones.com", "thismail.net", "throwam.com", "throwawaymail.com",
            "throwawaymail.net", "throwawaymail.pp.ua", "tilien.com", "tmail.ws",
            "tmpmail.net", "tmpmail.org", "toddsbighonkingpoll.com", "toiea.com",
            "tokem.co", "tormail.org", "tradermail.info", "trash-mail.at",
            "trash-mail.cf", "trash-mail.com", "trash-mail.de", "trash-mail.ga",
            "trash-mail.ml", "trash-mail.tk", "trash2009.com", "trash2010.com",
            "trashemail.de", "trashmail.at", "trashmail.com", "trashmail.de",
            "trashmail.io", "trashmail.me", "trashmail.net", "trashmail.ws",
            "trashymail.com", "trialmail.de", "trillianpro.com", "turbomail.to",
            "turbomailx.com", "uggsrock.com", "uhhu.ru", "umail.net",
            "upliftnow.com", "uwork4.us", "valhalladev.com", "venompen.com",
            "veryrealemail.com", "vidchart.com", "vipmail.name", "viralplays.com",
            "vomoto.com", "vpn.st", "vztc.com", "wasteland.rfc822.org",
            "watchever.net", "watchfull.net", "web-contact.info", "webemail.me",
            "webuser.in", "wegwerfadresse.de", "wegwerfmail.de", "wegwerfmail.net",
            "wegwerfmail.org", "wetrainbayarea.com", "wh4f.org", "whyspam.me",
            "wilemail.com", "willhackforfood.biz", "winemaven.info", "wins.com.br",
            "wuzup.net", "wuzupmail.net", "www.e4ward.com", "www.gmail.com",
            "wwwnew.eu", "x24.com", "xagloo.com", "xents.com", "xmail.com",
            "xmaily.com", "xn--9kq967o.com", "xoxy.net", "yep.it",
            "yogamaven.com", "yopmail.com", "yopmail.fr", "yopmail.net",
            "yourdomain.com", "ypmail.webarnak.fr.eu.org", "yuurok.com",
            "z1p.biz", "za.com", "zehnminutenmail.de", "zombie-hive.com",
            "zoemail.com", "zoemail.org", "zomg.info", "zxcv.com",
            "zzz.com"
        };

        private static readonly HashSet<string> DisposablePatterns = new(StringComparer.OrdinalIgnoreCase)
        {
            "temp", "throw", "trash", "spam", "fake", "burn", "disposable",
            "guerrilla", "mailinator", "yopmail", "10minute", "discard",
            "drop", "shark", "photon", "jetable", "temporary", "bogus"
        };

        public bool IsDisposableDomain(string domain)
        {
            if (string.IsNullOrWhiteSpace(domain))
                return false;

            // Verificacion exacta primero (mas rapido)
            if (DisposableDomains.Contains(domain))
                return true;

            // Verificar dominio padre para subdominios personalizados
            var parts = domain.Split('.');
            if (parts.Length > 2)
            {
                var parentDomain = string.Join('.', parts[^2], parts[^1]);
                if (DisposableDomains.Contains(parentDomain))
                    return true;
            }

            // Verificar patrones en el dominio
            foreach (var pattern in DisposablePatterns)
            {
                if (domain.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public int GetTotalKnownDomains() => DisposableDomains.Count;
    }
}
