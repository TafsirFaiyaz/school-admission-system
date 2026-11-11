import { useCallback, useEffect, useState, type FormEvent } from 'react';
import { request, type AdmissionClass, type CreatedHold, type StudentHold, type HoldStatus } from './api';

type View = 'classes' | 'mine' | 'admin';
const validStudent = (value: string) => /^[0-9]{17}$/.test(value);
const secondsRemaining = (expiresAt: string, clock: number) => Math.max(0, Math.ceil((new Date(expiresAt).getTime() - clock) / 1000));
const remainingLabel = (seconds: number) => `${String(Math.floor(seconds / 60)).padStart(2, '0')}:${String(seconds % 60).padStart(2, '0')}`;
function displayStatus(hold: StudentHold, clock: number): HoldStatus {
  return hold.status === 'HELD' && secondsRemaining(hold.expiresAt, clock) === 0 ? 'EXPIRED' : hold.status;
}

export default function App() {
  const [studentId, setStudentId] = useState<string>(() => localStorage.getItem('admission_student_id') || '');
  const [view, setView] = useState<View>('classes');
  const [classes, setClasses] = useState<AdmissionClass[]>([]);
  const [holds, setHolds] = useState<StudentHold[]>([]);
  const [selectedHold, setSelectedHold] = useState<string | null>(null);
  const [clock, setClock] = useState(Date.now());
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [className, setClassName] = useState('');
  const [seats, setSeats] = useState('120');
  const isValid = validStudent(studentId);

  const loadClasses = useCallback(async () => {
    const data = await request<AdmissionClass[]>('/classes');
    setClasses(data);
  }, []);
  const loadHolds = useCallback(async () => {
    if (!isValid) { setHolds([]); return; }
    const data = await request<StudentHold[]>('/me/holds', { studentId });
    setHolds(data);
  }, [studentId, isValid]);
  const refresh = useCallback(async () => {
    await Promise.all([loadClasses(), loadHolds()]);
  }, [loadClasses, loadHolds]);

  useEffect(() => { localStorage.setItem('admission_student_id', studentId); }, [studentId]);
  useEffect(() => {
    void refresh().catch((e: Error) => setError(e.message)).finally(() => setLoading(false));
    const polling = setInterval(() => void refresh().catch((e: Error) => setError(e.message)), 4000);
    const ticker = setInterval(() => setClock(Date.now()), 1000);
    return () => { clearInterval(polling); clearInterval(ticker); };
  }, [refresh]);

  const perform = async (operation: () => Promise<void>, success: string) => {
    setBusy(true); setError(''); setNotice('');
    try { await operation(); await refresh(); setNotice(success); }
    catch (e) { setError(e instanceof Error ? e.message : 'Unexpected error.'); }
    finally { setBusy(false); }
  };
  const onCreate = (event: FormEvent) => {
    event.preventDefault();
    void perform(async () => {
      await request('/classes', { method: 'POST', body: { name: className.trim(), totalSeats: Number(seats) } });
      setClassName(''); setView('classes');
    }, 'Admission class created successfully.');
  };
  const onApply = (classId: string) => void perform(async () => {
    const created = await request<CreatedHold>(`/classes/${classId}/holds`, { method: 'POST', studentId });
    setSelectedHold(created.holdId); setView('mine');
  }, 'Seat reserved. Complete the simulated payment before your reservation expires.');
  const onConfirm = (holdId: string) => void perform(async () => {
    await request(`/holds/${holdId}/confirm`, { method: 'POST', studentId });
  }, 'Admission confirmed successfully.');
  const onCancel = (holdId: string) => {
    if (!window.confirm('Cancel this reservation and release the seat?')) return;
    void perform(async () => {
      await request(`/holds/${holdId}`, { method: 'DELETE', studentId });
    }, 'Reservation cancelled; seat released.');
  };
  const selected = holds.find(h => h.holdId === selectedHold);

  return (
    <div className="site">
      <header className="topbar">
        <div className="brand"><div className="brandIcon">S</div><div><strong>School Admission</strong><small>Admissions management portal</small></div></div>
        <span className="headerTag">New academic year</span>
      </header>
      <main className="container">
        <section className="hero">
          <div><div className="eyebrow">ONLINE ADMISSIONS</div><h1>Apply with confidence.</h1><p>Browse open classes, reserve a seat, and confirm admission. Reservations remain active for a limited time.</p></div>
          <div className="studentPanel"><label htmlFor="student-id">Student birth registration number</label>
            <input id="student-id" autoComplete="off" value={studentId} onChange={e => setStudentId(e.target.value.replace(/[^0-9]/g, '').slice(0, 17))} placeholder="Enter 17-digit student ID" inputMode="numeric" maxLength={17}/>
            <small className={studentId.length > 0 && !isValid ? 'invalid' : ''}>{isValid ? '✓ Student ID ready' : 'Required for application, confirmation and history (17 digits)'}</small>
          </div>
        </section>
        <nav className="nav" aria-label="Portal sections">
          <button className={view === 'classes' ? 'active' : ''} onClick={() => setView('classes')}>Browse classes</button>
          <button className={view === 'mine' ? 'active' : ''} onClick={() => setView('mine')}>My applications <span className="counter">{holds.length}</span></button>
          <button className={view === 'admin' ? 'active' : ''} onClick={() => setView('admin')}>School admin</button>
        </nav>
        {error && <div className="alert alertError" role="alert"><span>{error}</span><button onClick={() => setError('')} aria-label="Dismiss error">×</button></div>}
        {notice && <div className="alert alertSuccess" role="status"><span>{notice}</span><button onClick={() => setNotice('')} aria-label="Dismiss notification">×</button></div>}
        {view === 'classes' && <section className="section">
          <div className="sectionHeading"><div><h2>Available admission classes</h2><p>Seat counts update automatically every few seconds.</p></div><span className="countPill">{classes.length} classes</span></div>
          {loading ? <p className="muted">Loading admission classes...</p> : classes.length === 0 ? <div className="empty">No admission classes have been created. Open School admin to add one.</div> :
            <div className="cards">{classes.map(c => <article className="classCard" key={c.id}>
              <div className="cardHead"><div className="schoolSymbol">▦</div><span className={c.availableSeats ? 'availability yes' : 'availability no'}>{c.availableSeats ? 'Seats available' : 'Currently full'}</span></div>
              <h3>{c.name}</h3><div className="stats"><div><strong>{c.totalSeats}</strong><span>Total</span></div><div><strong>{c.heldSeats}</strong><span>Held</span></div><div><strong>{c.confirmedSeats}</strong><span>Confirmed</span></div></div>
              <div className="bottomCard"><span><b>{c.availableSeats}</b> available</span><button disabled={busy || !isValid || c.availableSeats <= 0} onClick={() => onApply(c.id)}>{c.availableSeats ? 'Apply now' : 'Full'}</button></div>
            </article>)}</div>}
          {!isValid && <div className="hint">Enter a valid 17-digit student ID above to enable applications.</div>}
        </section>}
        {view === 'mine' && <section className="section">
          <div className="sectionHeading"><div><h2>My applications</h2><p>Your history is saved in the database and remains available after refreshing.</p></div><button className="secondaryButton" onClick={() => void refresh().catch((e: Error) => setError(e.message))}>Refresh</button></div>
          {!isValid ? <div className="empty">Enter a valid 17-digit student ID to see your applications.</div> : holds.length === 0 ? <div className="empty">No applications yet. Browse classes to submit your first application.</div> : <div className="holdList">{holds.map(h => {
            const status = displayStatus(h, clock); const remaining = secondsRemaining(h.expiresAt, clock);
            return <article className={`holdCard ${selectedHold === h.holdId ? 'selected' : ''}`} key={h.holdId}>
              <div className="holdMain"><div><div className="eyebrow">APPLICATION</div><h3>{h.className}</h3><p className="holdId">Hold: {h.holdId}</p></div><span className={`status status${status}`}>{status}</span></div>
              {status === 'HELD' && <div className="countdown"><span>Time left to confirm</span><strong>{remainingLabel(remaining)}</strong></div>}
              {status === 'EXPIRED' && <p className="muted">This reservation expired. Its seat is available to other applicants.</p>}
              {status === 'CONFIRMED' && <p className="muted">Your admission has been confirmed successfully.</p>}
              {status === 'CANCELLED' && <p className="muted">This reservation has been cancelled.</p>}
              {status === 'HELD' && <div className="holdActions"><button disabled={busy} onClick={() => onConfirm(h.holdId)}>Pay admission fee (simulate)</button><button className="outlineButton" disabled={busy} onClick={() => onCancel(h.holdId)}>Cancel hold</button></div>}
            </article>;
          })}</div>}
          {selected && <p className="hint">Your latest reservation is highlighted above.</p>}
        </section>}
        {view === 'admin' && <section className="section">
          <div className="sectionHeading"><div><h2>Create admission class</h2><p>School administrators can create classes and assign seat capacities.</p></div></div>
          <form className="adminForm" onSubmit={onCreate}>
            <label>Class and shift name<input required maxLength={150} value={className} onChange={e => setClassName(e.target.value)} placeholder="Class 6 - Morning Shift"/></label>
            <label>Number of seats<input required type="number" min="1" max="999999" value={seats} onChange={e => setSeats(e.target.value)}/></label>
            <button disabled={busy || !className.trim() || Number(seats) <= 0} type="submit">{busy ? 'Working...' : 'Create class'}</button>
            <p className="hint">Admin authentication is intentionally omitted because the assignment specifies a login-free demonstration.</p>
          </form>
        </section>}
      </main>
      <footer>School Admission System · First-come, first-served admission · Demo payment only</footer>
    </div>
  );
}
