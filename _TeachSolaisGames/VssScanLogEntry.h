/////////////////////////////////////////////////////////////////////////////
//
//	File: VssScanLogEntry.h
//
//	$Header:$
//
/////////////////////////////////////////////////////////////////////////////


#ifndef _VSS_SCAN_LOG_ENTRY_H_
#define _VSS_SCAN_LOG_ENTRY_H_


#include "BinaryReader.h"
#include "VssTypes.h"


class VssScanLogEntry
{
public:
	// !!!  WARNING -- THESE FIELDS ARE NOT DEFINED IN THE   !!!
	// !!! SAME ORDER THAT THEY OCCUR WITHIN THE BINARY DATA !!!

	// What type of operation is stored in this chunk?
	// This will map to one of the VssOpcode_... values.
	//
	u16 m_Opcode;

	// Each log entry is tagged with an incrementing version number.
	// The first entry starts at 1, and each subsequent entry increments.
	// This is the version number value that VSS displays when showing the
	// log of a file.
	//
	u16 m_VersionNumber;

	// This is a 32-bit time_t value, as defined in time.h.  Use one of
	// the time functions (such as gmtime(), localtime(), or asctime()) to
	// convert this into a more useful value.  Note that these timestamps
	// are in milliseconds, so it is difficult to recreate the exact order
	// of operations when several files are checked in at the same time.
	//
	// Also note: on my system, I have to use gmtime() to recover the
	// correct local time at which an operation occurred.  This implies
	// that all timestamps are in local time, not GMT, which could cause
	// problems when accessing VSS from machines that are in different
	// time zones.
	//
	u32 m_Timestamp;

	// This field indicates the absolute file offset of the previous log
	// entry chunk in the file.  Scanning the log of a file generally runs
	// backwards in time.  Start with the last log entry in the file, and
	// use m_PreviousOffset to seek to the start of the preceeding chunk.
	//
	u32 m_PreviousOffset;

	// Absolute offset at which the comment chunk is located.  Most types of
	// log entries will require a comment.  Even if the user did not type
	// one in, it will still create a comment chunk.  This field stores the
	// offset of the comment.
	//
	// Note: If a user edited a comment at a later date, this will not modify
	// the existing comment chunk.  Instead, a new comment chunk will be
	// appended to the end of the file, and m_CommentOffset will be updated
	// to point to the new comment chunk.
	//
	// In the few cases where there is no comment, this will be the offset of
	// the next chunk in the file.  However, since this value will be modified
	// when editing comments, you cannot rely upon this value to point to the
	// start of the next chunk.
	//
	// This field is only meaningful if m_CommentLength is non-zero.
	//
	u32 m_CommentOffset;

	// The length of the comment contained in the chunk referenced by the
	// m_CommentOffset field.  If m_CommentLength is zero, then there is no
	// comment.  However, most operations do require a comment, so a comment
	// chunk will always exist for them.  If the user did not type in a
	// comment when checking in the file, a 1-byte comment will be created
	// that contains the string "\0".
	//
	// This length appears to always include the '\0' terminator at the end
	// of the comment chunk.
	//
	u16 m_CommentLength;

	// A label operation is always followed by a comment chunk.  These two
	// fields indicate the position of the comment chunk for the label.
	// This comment chunk will normally occur immediately following the
	// label operation.  However, if someone edited the comment at a later
	// time, the offset will be that of the edited comment.
	//
	// Label comments are separate from regular comments, since a label may
	// have both types of comments.  For non-label operations, these fields
	// appear to always be zero.
	//
	u32 m_LabelOffset;
	u16 m_LabelLength;

	// Name of user who performed the operation.
	char m_Username[32];

	// This is only used for VssOpcode_Labeled operations.  It will contain
	// the label assigned to this file.
	//
	// Note that a label is only applied to the selected file or directory.
	// VSS will logically display that label when showing the change log of
	// child files/directories, but the label itself is not written into
	// any other files.  The exception is the "data\labels" directory,
	// which contains a file for every label ever created.  This appears to
	// be the information that is used when VSS shows labels in the history
	// dialog.  These small text files contain the path of the file that was
	// tagged, and a timestamp (this timestamp is a 32-bit time_t value, the
	// same as m_Timestamp).
	//
	char m_Label[32];

	// This field is used for VssOpcode_SharedFile and VssOpcode_CheckedIn
	// operations.  Note that this is a path within the VSS database, and
	// will start with "$/...".  Any path that starts with "$" will be a
	// reference to a project or file within the database.
	//
	// For VssOpcode_SharedFile, this contains the path of the file being
	// shared.
	//
	// For VssOpcode_CheckedIn, this is the path within the database from
	// which the check-in was performed.  This is really only relevant when
	// a file is shared between multiple projects.  m_DatabasePath will
	// indicate the project from which the check-in was performed.
	// It's not clear how this is useful, except perhaps for change auditing.
	// Within VSS itself, this information does not appear to be used for
	// anything.
	//
	char m_DatabasePath[260];

	// This is only used for VssOpcode_CheckedIn operations.  It indicates the
	// offset of the "FD" difference chunk for the check-in.
	u32 m_DifferenceOffset;

	// The name of the directory/file.  This appears to always be the current
	// name.  If the directory/file was renamed at some point, this will be
	// the name at the time the operation was performed.
	//
	// WARNING: This field is not used for check-in operations.
	// However, all other types of operations do have the name field filled in.
	//
	char m_FileName[34];

	// This is an offset in the \data\names.dat file.  This is used to store
	// filenames in the 8.3 short-name format (e.g., "reallylongname.txt" is
	// has a short file name of "really~1.txt").
	//
	// It's not clear exactly what VSS uses this for.  Possibly to make
	// certain that the exact same 8.3 name is used for a file when doing a
	// "get" operation into an empty directory.  I haven't had issues like
	// this since the FAT32 days -- is it still important for NTFS?
	//
	u32 m_NamesFileOffset;

	// These are only used when renaming a file or project.  Otherwise, they
	// are the same as m_FileName and m_NamesFileOffset.
	//
	char m_NewFileName[34];
	u32  m_NewFileNamesOffset;

	// This is the index of a file or directory entry within the m_pNodes[]
	// array.  Within VSS, this value is used to map back to the database's
	// "aaaaaaaa" file.  For ease of reference, this value has been converted
	// back to its integer representation.
	//
	// This will be set to -1 when it is not a valid reference.
	//
	s32 m_FileReference;

	// Only used for VssOpcode_BranchedFile operations.  This will be the ID
	// of the existing file that was branched.  The newly created branch will
	// have its ID stored in m_FileReference.
	//
	s32 m_BranchReference;

	u16		m_Type;
	u16		m_Flags;
	u16		m_NameFlags;
	u32		m_NameOffset;
	u16		m_PinnedID;
	char	m_DBName[10];

	VssScanLogEntry(void);

	void Scan(BinaryReader &reader);
	void Dump(void);
};


#endif // _VSS_SCAN_LOG_ENTRY_H_


