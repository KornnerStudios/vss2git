﻿/* Copyright 2017, Trapeze Poland sp. z o.o.
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

namespace Hpdi.Vss2Git.GitActions
{
    /// <summary>
    /// Represents a generic interface for replaying git action.
    /// </summary>
    /// <author>Dariusz Bywalec</author>
    interface IGitAction
    {
        bool Run(Logger logger, IGitWrapper git, IGitStatistic stat);
    }
}
