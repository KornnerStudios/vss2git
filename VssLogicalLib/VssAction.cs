﻿/* Copyright 2009 HPDI, LLC
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
using System.Collections.Generic;

namespace Hpdi.VssLogicalLib
{
    /// <summary>
    /// Enumeration of logical VSS revision actions.
    /// </summary>
    /// <author>Trevor Robinson</author>
    public enum VssActionType
    {
        Label,
        Create,
        Destroy,
        Add,
        Delete,
        Recover,
        Rename,
        MoveFrom,
        MoveTo,
        Share,
        Pin,
        Branch,
        Edit,
        Archive,
        Restore
    }

    /// <summary>
    /// Base class for VSS revision action descriptions.
    /// </summary>
    /// <author>Trevor Robinson</author>
    public abstract class VssAction
    {
        public abstract VssActionType Type { get; }
    }

    /// <summary>
    /// Represents a VSS label action.
    /// </summary>
    /// <author>Trevor Robinson</author>
    public sealed class VssLabelAction : VssAction
    {
        public override VssActionType Type => VssActionType.Label;

        public string Label { get; init; }

        public VssLabelAction(string label)
        {
            Label = label;
        }

        public override string ToString() => $"Label {Label}";
    }

    /// <summary>
    /// Base class for VSS project actions that target a particular item.
    /// </summary>
    /// <author>Trevor Robinson</author>
    public abstract class VssNamedAction : VssAction
    {
        public VssItemName Name { get; init; }

        public VssNamedAction(VssItemName name)
        {
            Name = name;
        }
    }

    /// <summary>
    /// Represents a VSS project/file destroy action.
    /// </summary>
    /// <author>Trevor Robinson</author>
    public sealed class VssDestroyAction : VssNamedAction
    {
        public override VssActionType Type => VssActionType.Destroy;

        public VssDestroyAction(VssItemName name)
            : base(name)
        {
        }

        public override string ToString() => $"Destroy {Name}";
    }

    /// <summary>
    /// Represents a VSS project/file create action.
    /// </summary>
    /// <author>Trevor Robinson</author>
    public sealed class VssCreateAction : VssNamedAction
    {
        public override VssActionType Type => VssActionType.Create;

        public VssCreateAction(VssItemName name)
            : base(name)
        {
        }

        public override string ToString() => $"Create {Name}";
    }

    /// <summary>
    /// Represents a VSS project/file add action.
    /// </summary>
    /// <author>Trevor Robinson</author>
    public sealed class VssAddAction : VssNamedAction
    {
        public override VssActionType Type => VssActionType.Add;

        public VssAddAction(VssItemName name)
            : base(name)
        {
        }

        public override string ToString() => $"Add {Name}";
    }

    /// <summary>
    /// Represents a VSS project/file delete action.
    /// </summary>
    /// <author>Trevor Robinson</author>
    public sealed class VssDeleteAction : VssNamedAction
    {
        public override VssActionType Type => VssActionType.Delete;

        public VssDeleteAction(VssItemName name)
            : base(name)
        {
        }

        public override string ToString() => $"Delete {Name}";
    }

    /// <summary>
    /// Represents a VSS project/file recover action.
    /// </summary>
    /// <author>Trevor Robinson</author>
    public sealed class VssRecoverAction : VssNamedAction
    {
        public override VssActionType Type => VssActionType.Recover;

        public VssRecoverAction(VssItemName name)
            : base(name)
        {
        }

        public override string ToString() => $"Recover {Name}";
    }

    /// <summary>
    /// Represents a VSS project/file rename action.
    /// </summary>
    /// <author>Trevor Robinson</author>
    public sealed class VssRenameAction : VssNamedAction
    {
        public override VssActionType Type => VssActionType.Rename;

        public string OriginalName { get; init; }

        public VssRenameAction(VssItemName name, string originalName)
            : base(name)
        {
            OriginalName = originalName;
        }

        public override string ToString() => $"Rename {OriginalName} to {Name}";
    }

    /// <summary>
    /// Represents a VSS project move-from action.
    /// </summary>
    /// <author>Trevor Robinson</author>
    public sealed class VssMoveFromAction : VssNamedAction
    {
        public override VssActionType Type => VssActionType.MoveFrom;

        public string OriginalProject { get; }

        public VssMoveFromAction(VssItemName name, string originalProject)
            : base(name)
        {
            OriginalProject = originalProject;
        }

        public override string ToString() => $"Move {Name} from {OriginalProject}";
    }

    /// <summary>
    /// Represents a VSS project move-to action.
    /// </summary>
    /// <author>Trevor Robinson</author>
    public sealed class VssMoveToAction : VssNamedAction
    {
        public override VssActionType Type => VssActionType.MoveTo;

        public string NewProject { get; }

        public VssMoveToAction(VssItemName name, string newProject)
            : base(name)
        {
            NewProject = newProject;
        }

        public override string ToString() => $"Move {Name} to {NewProject}";
    }

    /// <summary>
    /// Represents a VSS file share action.
    /// </summary>
    /// <author>Trevor Robinson</author>
    public sealed class VssShareAction : VssNamedAction
    {
        public override VssActionType Type => VssActionType.Share;

        public string OriginalProject { get; }

        public bool Pinned { get; }
        public int Revision { get; }

        public VssShareAction(VssItemName name, string originalProject, bool pinned, int revision)
            : base(name)
        {
            OriginalProject = originalProject;
            Pinned = pinned;
            Revision = revision;
        }

        public override string ToString() =>
            $"Share {Name} from {OriginalProject}, {(Pinned ? "Pin" : "Unpin")} at revision {Revision}";
    }

    /// <summary>
    /// Represents a VSS file pin/unpin action.
    /// </summary>
    /// <author>Trevor Robinson</author>
    public sealed class VssPinAction : VssNamedAction
    {
        public override VssActionType Type => VssActionType.Pin;

        public bool Pinned { get; }
        public int Revision { get; }

        public VssPinAction(VssItemName name, bool pinned, int revision)
            : base(name)
        {
            Pinned = pinned;
            Revision = revision;
        }

        public override string ToString() =>
            $"{(Pinned ? "Pin" : "Unpin")} {Name} at revision {Revision}";
    }

    /// <summary>
    /// Represents a VSS file branch action.
    /// </summary>
    /// <author>Trevor Robinson</author>
    public sealed class VssBranchAction : VssNamedAction
    {
        public override VssActionType Type => VssActionType.Branch;

        public VssItemName Source { get; }

        public VssBranchAction(VssItemName name, VssItemName source)
            : base(name)
        {
            Source = source;
        }

        public override string ToString() => $"Branch {Name} from {Source.PhysicalName}";
    }

    /// <summary>
    /// Represents a VSS file edit action.
    /// </summary>
    /// <author>Trevor Robinson</author>
    public sealed class VssEditAction : VssAction
    {
        public override VssActionType Type => VssActionType.Edit;

        public string PhysicalName { get; }

        public VssEditAction(string physicalName)
        {
            PhysicalName = physicalName;
        }

        public override string ToString() => $"Edit {PhysicalName}";
    }

    /// <summary>
    /// Represents a VSS archive action.
    /// </summary>
    /// <author>Trevor Robinson</author>
    public sealed class VssArchiveAction : VssNamedAction
    {
        public override VssActionType Type => VssActionType.Archive;

        public string ArchivePath { get; }

        public VssArchiveAction(VssItemName name, string archivePath)
            : base(name)
        {
            ArchivePath = archivePath;
        }

        public override string ToString() => $"Archive {Name} to {ArchivePath}";
    }

    /// <summary>
    /// Represents a VSS restore from archive action.
    /// </summary>
    /// <author>Trevor Robinson</author>
    public sealed class VssRestoreAction : VssNamedAction
    {
        public override VssActionType Type => VssActionType.Restore;

        public string ArchivePath { get; }

        public VssRestoreAction(VssItemName name, string archivePath)
            : base(name)
        {
            this.ArchivePath = archivePath;
        }

        public override string ToString() => $"Restore {Name} from archive {ArchivePath}";
    }
}
