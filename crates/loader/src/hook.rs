//! Minimal x86 inline hook: overwrite a function's first instructions with a
//! jump to the detour, and keep them in a trampoline so the original stays callable.

use crate::win::*;

const JMP_REL32_LEN: usize = 5;
const MAX_STOLEN: usize = 16;

/// Follows the thunks kernel32 uses to forward to kernelbase, so the hook lands
/// on the code every caller eventually reaches.
unsafe fn resolve_thunks(mut p: *const u8) -> *const u8 {
    loop {
        match (*p, *p.add(1)) {
            (0xE9, _) => {
                let rel = (p.add(1) as *const i32).read_unaligned();
                p = p.add(JMP_REL32_LEN).offset(rel as isize);
            }
            (0xFF, 0x25) => {
                let slot = (p.add(2) as *const *const *const u8).read_unaligned();
                p = slot.read_unaligned();
            }
            _ => return p,
        }
    }
}

/// Length of one instruction, for the position-independent ones found in Win32
/// function prologues. Anything else is refused rather than guessed.
unsafe fn insn_len(p: *const u8) -> Option<usize> {
    Some(match *p {
        0x50..=0x57 | 0x90 => 1,
        0x6A => 2,
        0x68 => 5,
        0x8B | 0x33 | 0x31 | 0x85 if *p.add(1) >= 0xC0 => 2,
        0x83 if *p.add(1) == 0xEC => 3,
        0x81 if *p.add(1) == 0xEC => 6,
        _ => return None,
    })
}

/// Installs the hook and returns the trampoline that calls the original function.
pub unsafe fn install(target: *const u8, detour: *const u8) -> Result<*const u8, String> {
    let target = resolve_thunks(target);
    let mut stolen = 0;
    while stolen < JMP_REL32_LEN {
        stolen += insn_len(target.add(stolen)).ok_or_else(|| {
            let bytes = std::slice::from_raw_parts(target, MAX_STOLEN);
            format!("unsupported prologue at {target:p}: {bytes:02x?}")
        })?;
    }

    let trampoline = VirtualAlloc(
        std::ptr::null_mut(),
        stolen + JMP_REL32_LEN,
        MEM_COMMIT | MEM_RESERVE,
        PAGE_EXECUTE_READWRITE,
    ) as *mut u8;
    if trampoline.is_null() {
        return Err("VirtualAlloc failed".into());
    }
    std::ptr::copy_nonoverlapping(target, trampoline, stolen);
    write_jmp(trampoline.add(stolen), target.add(stolen));

    let mut old = 0;
    if VirtualProtect(target as *mut _, stolen, PAGE_EXECUTE_READWRITE, &mut old) == 0 {
        return Err("VirtualProtect failed".into());
    }
    let site = target as *mut u8;
    write_jmp(site, detour);
    site.add(JMP_REL32_LEN).write_bytes(0x90, stolen - JMP_REL32_LEN);
    VirtualProtect(target as *mut _, stolen, old, &mut old);
    FlushInstructionCache(GetCurrentProcess(), target as *const _, stolen);
    Ok(trampoline)
}

unsafe fn write_jmp(at: *mut u8, to: *const u8) {
    *at = 0xE9;
    let rel = (to as isize - at.add(JMP_REL32_LEN) as isize) as i32;
    (at.add(1) as *mut i32).write_unaligned(rel);
}
