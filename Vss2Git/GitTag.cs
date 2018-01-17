using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hpdi.Vss2Git
{
    class GitTag
    {
        private readonly string name;
        private readonly string taggerName;
        private readonly string taggerEmail;
        private readonly string message;
        private readonly DateTime utcTime;

        public GitTag(string name, string taggerName, string taggerEmail, string message, DateTime utcTime)
        {
            this.name = name;
            this.taggerName = taggerName;
            this.taggerEmail = taggerEmail;
            this.message = message;
            this.utcTime = utcTime;
        }

        public bool Run(IGitWrapper git)
        {
            git.Tag(name, taggerName, taggerEmail, message, utcTime);
            return true;
        }
    }
}
