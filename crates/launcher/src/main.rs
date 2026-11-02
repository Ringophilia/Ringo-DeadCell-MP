//! Starts Dead Cells with the mod loaded. Meant as a Steam launch option:
//!
//!     "G:\path\to\ringo-launcher.exe" %command%
//!
//! Steam then starts us with the game's environment and we start the game as a
//! child. Removing the launch option brings back the unmodified game.
//!
//! The DLL has to be in place before the game's boot code asks for its bytecode.
//! At CREATE_SUSPENDED the process loader has not run yet, so we first park the
//! main thread on the entry point, where the loader is done and no game code
//! has run, inject there, then let it go.

mod win;

use std::ffi::{c_void, OsStr, OsString};
use std::os::windows::ffi::OsStrExt;
use std::path::{Path, PathBuf};
use std::ptr::null_mut;
use std::time::{Duration, Instant};
use win::*;

const JMP_SELF: [u8; 2] = [0xEB, 0xFE];
const ENTRY_TIMEOUT: Duration = Duration::from_secs(15);
const PEB_IMAGE_BASE: usize = 8;

fn wide(s: &OsStr) -> Vec<u16> {
    s.encode_wide().chain(Some(0)).collect()
}

fn quote(arg: &OsStr) -> OsString {
    let mut out = OsString::from("\"");
    out.push(arg);
    out.push("\"");
    out
}

unsafe fn read<T: Copy>(process: Handle, addr: usize) -> Result<T, String> {
    let mut value = std::mem::MaybeUninit::<T>::uninit();
    let n = std::mem::size_of::<T>();
    if ReadProcessMemory(process, addr as *const c_void, value.as_mut_ptr() as *mut c_void, n, null_mut()) == 0 {
        return Err(format!("cannot read {n} bytes at {addr:#x}"));
    }
    Ok(value.assume_init())
}

unsafe fn entry_point(process: Handle) -> Result<usize, String> {
    let mut info = ProcessBasicInformation::default();
    let size = std::mem::size_of::<ProcessBasicInformation>() as u32;
    let status = NtQueryInformationProcess(process, 0, &mut info as *mut _ as *mut c_void, size, null_mut());
    if status != 0 {
        return Err(format!("NtQueryInformationProcess: {status:#x}"));
    }
    let base: u32 = read(process, info.peb + PEB_IMAGE_BASE)?;
    let base = base as usize;
    let pe: u32 = read(process, base + 0x3C)?;
    let rva: u32 = read(process, base + pe as usize + 0x28)?;
    Ok(base + rva as usize)
}

unsafe fn patch(process: Handle, addr: usize, bytes: &[u8]) -> Result<(), String> {
    let mut old = 0;
    let p = addr as *mut c_void;
    if VirtualProtectEx(process, p, bytes.len(), PAGE_EXECUTE_READWRITE, &mut old) == 0 {
        return Err(format!("VirtualProtectEx failed at {addr:#x}"));
    }
    let ok = WriteProcessMemory(process, p, bytes.as_ptr() as *const c_void, bytes.len(), null_mut());
    VirtualProtectEx(process, p, bytes.len(), old, &mut old);
    FlushInstructionCache(process, p, bytes.len());
    if ok == 0 {
        return Err(format!("WriteProcessMemory failed at {addr:#x}"));
    }
    Ok(())
}

/// Lets the process loader finish, then holds the main thread at the entry point.
unsafe fn park_at_entry(process: Handle, thread: Handle, entry: usize) -> Result<[u8; 2], String> {
    let original: [u8; 2] = read(process, entry)?;
    patch(process, entry, &JMP_SELF)?;
    ResumeThread(thread);
    let deadline = Instant::now() + ENTRY_TIMEOUT;
    let mut ctx: Box<Context> = Box::new(std::mem::zeroed());
    loop {
        std::thread::sleep(Duration::from_millis(1));
        SuspendThread(thread);
        ctx.flags = CONTEXT_CONTROL;
        let at_entry = GetThreadContext(thread, &mut *ctx) != 0 && ctx.eip as usize == entry;
        if at_entry {
            return Ok(original);
        }
        ResumeThread(thread);
        if Instant::now() > deadline {
            return Err("the game never reached its entry point".into());
        }
    }
}

unsafe fn inject(process: Handle, dll: &Path) -> Result<(), String> {
    let path = wide(dll.as_os_str());
    let bytes = path.len() * 2;
    let remote = VirtualAllocEx(process, null_mut(), bytes, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
    if remote.is_null() {
        return Err("VirtualAllocEx failed".into());
    }
    if WriteProcessMemory(process, remote, path.as_ptr() as *const c_void, bytes, null_mut()) == 0 {
        return Err("cannot write the DLL path".into());
    }
    // kernel32 sits at the same address in every 32-bit process of this session.
    let kernel32 = GetModuleHandleW(wide(OsStr::new("kernel32.dll")).as_ptr());
    let load_library = GetProcAddress(kernel32, b"LoadLibraryW\0".as_ptr());
    let thread = CreateRemoteThread(process, null_mut(), 0, load_library, remote, 0, null_mut());
    if thread.is_null() {
        return Err("CreateRemoteThread failed".into());
    }
    WaitForSingleObject(thread, INFINITE);
    let mut module = 0;
    GetExitCodeThread(thread, &mut module);
    if module == 0 {
        return Err(format!("the game could not load {}", dll.display()));
    }
    Ok(())
}

fn beside_launcher(file: &str) -> Result<PathBuf, String> {
    let exe = std::env::current_exe().map_err(|e| e.to_string())?;
    Ok(exe.with_file_name(file))
}

fn run() -> Result<u32, String> {
    let args: Vec<OsString> = std::env::args_os().skip(1).collect();
    let (game, game_args) = args.split_first().ok_or("usage: ringo-launcher <deadcells.exe> [args]")?;
    let game = PathBuf::from(game);
    let dir = game.parent().ok_or("the game path has no folder")?.to_owned();
    let dll = beside_launcher("ringomp.dll")?;
    if !dll.exists() {
        return Err(format!("{} is missing", dll.display()));
    }
    if std::env::var_os("RINGO_BYTECODE").is_none() {
        std::env::set_var("RINGO_BYTECODE", beside_launcher("ringomp.hlb")?);
    }
    if std::env::var_os("RINGO_LOG").is_none() {
        std::env::set_var("RINGO_LOG", beside_launcher("ringomp.log")?);
    }

    let mut cmd = quote(game.as_os_str());
    for arg in game_args {
        cmd.push(" ");
        cmd.push(quote(arg));
    }
    let mut cmd = wide(&cmd);
    let cwd = wide(dir.as_os_str());
    let exe = wide(game.as_os_str());

    unsafe {
        let mut si: StartupInfoW = std::mem::zeroed();
        si.cb = std::mem::size_of::<StartupInfoW>() as u32;
        let mut pi = ProcessInformation::default();
        let ok = CreateProcessW(
            exe.as_ptr(),
            cmd.as_mut_ptr(),
            null_mut(),
            null_mut(),
            0,
            CREATE_SUSPENDED,
            null_mut(),
            cwd.as_ptr(),
            &mut si,
            &mut pi,
        );
        if ok == 0 {
            return Err(format!("cannot start {}: {}", game.display(), std::io::Error::last_os_error()));
        }
        let (process, thread) = (pi.process as Handle, pi.thread as Handle);
        let prepared = (|| {
            let entry = entry_point(process)?;
            let original = park_at_entry(process, thread, entry)?;
            inject(process, &dll)?;
            patch(process, entry, &original)
        })();
        if let Err(e) = prepared {
            TerminateProcess(process, 1);
            return Err(e);
        }
        ResumeThread(thread);
        WaitForSingleObject(process, INFINITE);
        let mut code = 0;
        GetExitCodeProcess(process, &mut code);
        Ok(code)
    }
}

fn main() {
    match run() {
        Ok(code) => std::process::exit(code as i32),
        Err(e) => {
            eprintln!("ringo-launcher: {e}");
            std::process::exit(1);
        }
    }
}
