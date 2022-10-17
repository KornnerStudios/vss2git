/////////////////////////////////////////////////////////////////////////////
//
//	File: VssScanCheckout.h
//
//	$Header:$
//
/////////////////////////////////////////////////////////////////////////////


#ifndef _VSS_SCAN_CHECKOUT_H_
#define _VSS_SCAN_CHECKOUT_H_


#include "BinaryReader.h"
#include "VssTypes.h"


class VssScanCheckout
{
public:
	// Note that these strings are only used after a file has been checked
	// out.  For newly-created files, these strings are empty.  (This also
	// appears to be true for a file after it has been shared -- the shared
	// link starts out with no check-out strings.)  The strings are retained
	// after the file is checked in, providing a record of who made the most
	// recent change to the file.

	// Name of the user who currently holds the check-out on the file, or
	// performed the last check-in.
	//
	char m_Username[32];

	// Network name for the machine where the file is checked out.
	//
	char m_Machine[32];

	// Absolute path ("D:\foo\bar.h") at which the file is checked out.
	//
	char m_Filename[260];

	// This stores the path to the file within VSS, which can be used to
	// disambiguate which link is being used when the file is shared between
	// multiple projects.
	//
	// This always starts with // "$/", which indicates the root of the VSS
	// source tree.
	//
	char m_Project[260];

	// When a file is checked out, the user is (usually) prompted to enter a
	// comment.  That string is stored here, and will serve as the default
	// comment when the file is eventually checked in.
	//
	char m_Comment[64];

	// This 32-bit word is always zero in my test database.  But the check-out
	// chunk only records one check-out at a time.  How does VSS track when
	// there is more than one check-out?  Considering how other chunks track
	// data, my suspicion is this...
	//
	// This word stores the offset of the next check-out chunk.  By default,
	// this is zero, indicating that there are no further check-out chunks.
	// Additional chunks are created when a file is checked out by more than
	// one person at a time.  To determine if a file is checked out, you need
	// to use this value to traverse through the file, looking for all of the
	// check-out chunks.
	//
	// However, the database used to test this code has only ever been used
	// in a non-networking environment, so multiple check-outs were never
	// performed.
	//
	u32 m_NextCheckout;

	// This is the time at which the file was last checked-out.  (Is it
	// updated when the file is checked in?)  This is a 32-bit time_t value.
	// 
	u32 m_CheckoutTime;

	// If the file is checked out, this indicates the version at which the
	// file was checked out.
	//
	// If the file is not checked out, this is zero.
	//
	u16 m_CheckoutVersion;

	// This field is always set to 0x40 when a file is checked out.
	// If the file is not checked out, it is zero.
	//
	// No other values have been observed here, but there may be flags
	// defined in case the file is currently checked out multiple times?
	//
	u16 m_CheckoutFlag;

	// The version number applied to the most recent check-in.  For a
	// newly-created file, this is zero.  It looks like a file that has been
	// created, checked out once, but not yet checked in, will also still
	// have this value set to zero.
	//
	u16 m_CheckinVersion;

	// This value is usually set to 0x01A0, which is the offset within the
	// file at which this check-out chunk is located.  Sometimes, it is zero.
	// If the file has never been subjected to a check-out (either it is a
	// new file, or has been shared), this will be zero.  Some files that
	// have been checked in will also have this set to zero if the file has
	// been shared or branched.  But it does not appear to be consistent, so
	// I cannot determine what the pattern is.  This may be a symptom of VSS
	// writing uninitialized memory to disk.
	//
	u16 m_Flag1;

	// This is usually 0x1000.  Sometimes it is zero.  Usually, it is zero
	// when m_Flag1 is zero, and it is 0x1000 when m_Flag1 is 0x01A0.  But
	// there are rare cases where this is zero even when m_Flag1 is 0x01A0.
	// No pattern could be discerned for the handful of exceptions that were
	// found.
	//
	u16 m_Flag2;

	bool Scan(BinaryReader &reader);
	void Dump(void);
};


#endif // _VSS_SCAN_CHECKOUT_H_


