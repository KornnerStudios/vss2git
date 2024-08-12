/* Copyright 2017, Trapeze Poland sp. z o.o.
 *
 * Author: Dariusz Bywalec
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;

namespace Hpdi.Vss2Git.GitActions
{
    /// <summary>
    /// Represents a git tag action.
    /// </summary>
    /// <author>Dariusz Bywalec</author>
    class CreateTag : IGitAction
    {
        private readonly string tag;
        private readonly string taggerName;
        private readonly string taggerEmail;
        private readonly string message;
        private readonly DateTime utcTime;

        public CreateTag(string tag, string taggerName, string taggerEmail, string message, DateTime utcTime)
        {
            this.tag = tag;
            this.taggerName = taggerName;
            this.taggerEmail = taggerEmail;
            this.message = message;
            this.utcTime = utcTime;
        }

        public bool Run(
            SourceSafe.IO.SimpleLogger logger,
            IGitWrapper git,
            IGitStatistic stat)
        {
            logger.WriteLine("Creating git tag: {0}", tag);

            git.Tag(tag, taggerName, taggerEmail, message, utcTime);

            stat.AddTag();

            return true;
        }
    }
}
