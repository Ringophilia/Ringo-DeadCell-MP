//! Injected into deadcells.exe before its entry point runs.
//!
//! The exe's HashLink boot code asks for the `hlboot.dat` RCDATA resource and,
//! when found, ignores any bytecode file on the command line. It calls
//! FindResourceW, SizeofResource and LoadResource, then hands the pointer
//! straight to hl_code_read. Answering those three calls is all it takes to run
//! our bytecode, and the game's files stay untouched.

mod hook;
mod win;

use std::ffi::c_void;
use std::io::Write;
use std::ptr::{null, null_mut};
use std::sync::atomic::{AtomicPtr, AtomicUsize, Ordering};
use win::*;

const RT_RCDATA: usize = 10;
/// A resource handle nothing else can produce.
const FAKE_RESOURCE: Handle = 0x5249_4E47 as Handle;

type FindResourceW = unsafe extern "system" fn(Handle, *const u16, *const u16) -> Handle;
type SizeofResource = unsafe extern "system" fn(Handle, Handle) -> u32;
type LoadResource = unsafe extern "system" fn(Handle, Handle) -> Handle;

static ORIG_FIND: AtomicUsize = AtomicUsize::new(0);
static ORIG_SIZE: AtomicUsize = AtomicUsize::new(0);
static ORIG_LOAD: AtomicUsize = AtomicUsize::new(0);
static BYTECODE: AtomicPtr<u8> = AtomicPtr::new(null_mut());
static BYTECODE_LEN: AtomicUsize = AtomicUsize::new(0);

fn log(msg: &str) {
    let Some(path) = std::env::var_os("RINGO_LOG") else { return };
    if let Ok(mut f) = std::fs::OpenOptions::new().create(true).append(true).open(path) {
        let _ = writeln!(f, "[loader] {msg}");
    }
}

/// Reads the bytecode the launcher pointed us at. Without one the game boots
/// untouched, so a missing file degrades to vanilla instead of a crash.
fn load_bytecode() -> bool {
    if !BYTECODE.load(Ordering::Acquire).is_null() {
        return true;
    }
    let Some(path) = std::env::var_os("RINGO_BYTECODE") else {
        log("RINGO_BYTECODE not set, running vanilla bytecode");
        return false;
    };
    match std::fs::read(&path) {
        Ok(data) if data.starts_with(b"HLB") => {
            log(&format!("serving {} bytes from {}", data.len(), path.to_string_lossy()));
            BYTECODE_LEN.store(data.len(), Ordering::Release);
            BYTECODE.store(Box::leak(data.into_boxed_slice()).as_mut_ptr(), Ordering::Release);
            true
        }
        Ok(_) => {
            log(&format!("{} is not HashLink bytecode", path.to_string_lossy()));
            false
        }
        Err(e) => {
            log(&format!("cannot read {}: {e}", path.to_string_lossy()));
            false
        }
    }
}

unsafe fn is_hlboot(name: *const u16, kind: *const u16) -> bool {
    const NAME: &[u8] = b"hlboot.dat\0";
    // Integer resource ids are passed in the low word of the pointer.
    if kind as usize != RT_RCDATA || (name as usize) >> 16 == 0 {
        return false;
    }
    NAME.iter().enumerate().all(|(i, &c)| *name.add(i) == c as u16)
}

unsafe extern "system" fn find_resource(m: Handle, name: *const u16, kind: *const u16) -> Handle {
    if is_hlboot(name, kind) && load_bytecode() {
        return FAKE_RESOURCE;
    }
    let orig: FindResourceW = std::mem::transmute(ORIG_FIND.load(Ordering::Acquire));
    orig(m, name, kind)
}

unsafe extern "system" fn sizeof_resource(m: Handle, res: Handle) -> u32 {
    if res == FAKE_RESOURCE {
        return BYTECODE_LEN.load(Ordering::Acquire) as u32;
    }
    let orig: SizeofResource = std::mem::transmute(ORIG_SIZE.load(Ordering::Acquire));
    orig(m, res)
}

unsafe extern "system" fn load_resource(m: Handle, res: Handle) -> Handle {
    if res == FAKE_RESOURCE {
        log("hlboot.dat handed to HashLink");
        return BYTECODE.load(Ordering::Acquire) as Handle;
    }
    let orig: LoadResource = std::mem::transmute(ORIG_LOAD.load(Ordering::Acquire));
    orig(m, res)
}

unsafe fn install_all() -> Result<(), String> {
    let name: Vec<u16> = "kernel32.dll\0".encode_utf16().collect();
    let kernel32 = GetModuleHandleW(name.as_ptr());
    let hook = |symbol: &[u8], detour: *const u8, slot: &AtomicUsize| -> Result<(), String> {
        let target = GetProcAddress(kernel32, symbol.as_ptr());
        if target == null() {
            return Err(format!("{} not found", String::from_utf8_lossy(symbol)));
        }
        slot.store(hook::install(target, detour)? as usize, Ordering::Release);
        Ok(())
    };
    hook(b"FindResourceW\0", find_resource as *const u8, &ORIG_FIND)?;
    hook(b"SizeofResource\0", sizeof_resource as *const u8, &ORIG_SIZE)?;
    hook(b"LoadResource\0", load_resource as *const u8, &ORIG_LOAD)?;
    Ok(())
}

#[no_mangle]
unsafe extern "system" fn DllMain(_module: Handle, reason: u32, _reserved: *mut c_void) -> i32 {
    if reason == DLL_PROCESS_ATTACH {
        match install_all() {
            Ok(()) => log("hooks installed"),
            Err(e) => {
                log(&format!("hook install failed: {e}"));
                return 0;
            }
        }
    }
    1
}
