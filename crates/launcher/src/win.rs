//! The Win32 declarations the launcher needs.
#![allow(non_snake_case)]

use std::ffi::c_void;

pub type Handle = *mut c_void;

pub const CREATE_SUSPENDED: u32 = 0x4;
pub const INFINITE: u32 = 0xFFFF_FFFF;
pub const MEM_COMMIT: u32 = 0x1000;
pub const MEM_RESERVE: u32 = 0x2000;
pub const PAGE_READWRITE: u32 = 0x04;
pub const PAGE_EXECUTE_READWRITE: u32 = 0x40;
pub const CONTEXT_CONTROL: u32 = 0x0001_0001;

#[repr(C)]
pub struct StartupInfoW {
    pub cb: u32,
    pub reserved: [usize; 2],
    pub title: *mut u16,
    pub pos_size: [u32; 7],
    pub flags: u32,
    pub show: u16,
    pub reserved2: u16,
    pub reserved3: *mut u8,
    pub std: [Handle; 3],
}

#[repr(C)]
#[derive(Default)]
pub struct ProcessInformation {
    pub process: usize,
    pub thread: usize,
    pub process_id: u32,
    pub thread_id: u32,
}

/// i386 CONTEXT; only the control registers are read.
#[repr(C, align(4))]
pub struct Context {
    pub flags: u32,
    pub before_eip: [u8; 0xB4],
    pub eip: u32,
    pub after_eip: [u8; 0x2CC - 0xBC],
}

#[repr(C)]
#[derive(Default)]
pub struct ProcessBasicInformation {
    pub exit_status: usize,
    pub peb: usize,
    pub rest: [usize; 4],
}

#[link(name = "kernel32")]
extern "system" {
    pub fn CreateProcessW(
        app: *const u16,
        cmd: *mut u16,
        pattr: *mut c_void,
        tattr: *mut c_void,
        inherit: i32,
        flags: u32,
        env: *mut c_void,
        cwd: *const u16,
        si: *mut StartupInfoW,
        pi: *mut ProcessInformation,
    ) -> i32;
    pub fn ResumeThread(thread: Handle) -> u32;
    pub fn SuspendThread(thread: Handle) -> u32;
    pub fn GetThreadContext(thread: Handle, ctx: *mut Context) -> i32;
    pub fn WaitForSingleObject(h: Handle, ms: u32) -> u32;
    pub fn GetExitCodeProcess(h: Handle, code: *mut u32) -> i32;
    pub fn GetExitCodeThread(h: Handle, code: *mut u32) -> i32;
    pub fn TerminateProcess(h: Handle, code: u32) -> i32;
    pub fn ReadProcessMemory(h: Handle, addr: *const c_void, buf: *mut c_void, n: usize, got: *mut usize) -> i32;
    pub fn WriteProcessMemory(h: Handle, addr: *mut c_void, buf: *const c_void, n: usize, put: *mut usize) -> i32;
    pub fn VirtualAllocEx(h: Handle, addr: *mut c_void, size: usize, kind: u32, protect: u32) -> *mut c_void;
    pub fn VirtualProtectEx(h: Handle, addr: *mut c_void, size: usize, protect: u32, old: *mut u32) -> i32;
    pub fn FlushInstructionCache(h: Handle, addr: *const c_void, size: usize) -> i32;
    pub fn CreateRemoteThread(
        h: Handle,
        attr: *mut c_void,
        stack: usize,
        start: *const c_void,
        param: *mut c_void,
        flags: u32,
        id: *mut u32,
    ) -> Handle;
    pub fn GetModuleHandleW(name: *const u16) -> Handle;
    pub fn GetProcAddress(module: Handle, name: *const u8) -> *const c_void;
}

#[link(name = "ntdll")]
extern "system" {
    pub fn NtQueryInformationProcess(h: Handle, class: u32, info: *mut c_void, len: u32, ret: *mut u32) -> i32;
}
