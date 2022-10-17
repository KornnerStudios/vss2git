/////////////////////////////////////////////////////////////////////////////
//
//	File: VssScanCheckout.cpp
//
//	$Header:$
//
/////////////////////////////////////////////////////////////////////////////


#include "Common.h"
#include "VssScanCheckout.h"


#ifdef _DEBUG
#define new DEBUG_NEW
#undef THIS_FILE
static char THIS_FILE[] = __FILE__;
#endif


/////////////////////////////////////////////////////////////////////////////
//
//	Scan()
//
bool VssScanCheckout::Scan(BinaryReader &reader)
{
	reader.ReadData(m_Username, 32);

	m_CheckoutTime = reader.Read32();

	reader.ReadData(m_Filename, 260);

	reader.ReadData(m_Machine, 32);

	reader.ReadData(m_Project, 260);

	reader.ReadData(m_Comment, 64);

	m_CheckoutVersion = reader.Read16();	// if checked out, this is the version number, otherwise it is zero
	m_CheckoutFlag    = reader.Read16();	// 0x40 if checked out, 0 otherwise
	m_NextCheckout    = reader.Read32();	// always zero
	m_Flag1           = reader.Read16();	// sometimes 0, usually 0x1A0 (which is the offset of this chunk in the file)
	m_Flag2           = reader.Read16();	// usually 0x1000, sometimes zero

	// Note: If a file has been created, but never checked out, all of these
	// fields will be zero.  Some of the fields are zero even if they have
	// been checked in.  More detail comments are written up in the header
	// file.

	m_CheckinVersion = reader.Read16();

	return true;
}


/////////////////////////////////////////////////////////////////////////////
//
//	Dump()
//
void VssScanCheckout::Dump(void)
{
//	printf("checkout\n");
//	printf("\"%s\" \"%s\" \"%s\" \"%s\"\n", m_Username, m_Machine, m_Filename, m_Project);
	printf("%04X %04X %04X %04X %04X %04X\n", m_CheckoutTime, m_CheckoutVersion, m_CheckoutFlag, m_CheckinVersion, m_Flag1, m_Flag2);

	// 0x00 indicates the file is not checked out.
	// 0x40 indicates the file is checked out.
	// No other value has been observed.
	if (m_CheckoutFlag && (0x40 != m_CheckoutFlag)) {
		printf("unexpected checkout flag\n");
	}
}
