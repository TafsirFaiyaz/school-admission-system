import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react';
import { request, type AdmissionClass, type CreatedHold, type StudentHold, type HoldStatus } from './api';

type View = 'classes' | 'mine' | 'admin';

// The assignment accepts a birth registration number as a 17-digit student identity.
const normalizeStudentId = (value: string) => value.replace(/[^0-9]/g, '').slice(0, 17);
const validStudent = (value: string) => /^[0-9]{17}$/.test(value);
const secondsRemaining = (expiresAt: string, clock: number) =>
  Math.max(0, Math.ceil((new Date(expiresAt).getTime() - clock) / 1000));
const remainingLabel = (seconds: number) =>
  `${String(Math.floor(seconds / 60)).padStart(2, '0')}:${String(seconds % 60).padStart(2, '0')}`;

function displayStatus(hold: StudentHold, clock: number): HoldStatus {
  return hold.status === 'HELD' && secondsRemaining(hold.expiresAt, clock) === 0
    ? 'EXPIRED' : hold.status;
}

export default function App() {
  const [studentId, setStudentId] = useState<string>(() =>
    normalizeStudentId(localStorage.getItem('admission_student_id') || ''));
  const [view, setView] = useState<View>('classes');
  const [classes, setClasses] = useState<AdmissionClass[]>([]);
  const [holds, setHolds] = useState<StudentHold[]>([]);
  const [selectedHold, setSelectedHold] = useState<string | null>(null);
  const [clock, setClock] = useState(Date.now());
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [syncWarning, setSyncWarning] = useState('');
  const [className, setClassName] = useState('');
  const [seats, setSeats] = useState('120');
  const [applyClass, setApplyClass] = useState<AdmissionClass | null>(null);
  const [draftStudentId, setDraftStudentId] = useState('');
  const [applyError, setApplyError] = useState('');
  const applyInput = useRef<HTMLInputElement>(null);
  const refreshSequence = useRef(0);
  const isValid = validStudent(studentId);

  // Fetch class list and student applications through the same configured API origin.
  // A changing student ID cannot let an earlier poll overwrite newer results.
  const refresh = useCallback(async (id: string = studentId) => {
    const sequence = ++refreshSequence.current;
    const [nextClasses, nextHolds] = await Promise.all([
      request<AdmissionClass[]>('/classes'),
      validStudent(id)
        ? request<StudentHold[]>('/me/holds', { studentId: id })
        : Promise.resolve([] as StudentHold[])
    ]);
    if (sequence !== refreshSequence.current) return;
    setClasses(nextClasses);
    setHolds(nextHolds);
    const currentClassIds = new Set(nextClasses.map(c => c.id));
    const missingClass = nextHolds.some(h => !currentClassIds.has(h.classId));
    setSyncWarning(missingClass
      ? 'An application refers to a class missing from the class list. The two responses should come from the same database. Check the API routing and Docker services using CHECK_CONNECTION.ps1.'
      : '');
  }, [studentId]);

  useEffect(() => {
    localStorage.setItem('admission_student_id', studentId);
  }, [studentId]);

  useEffect(() => {
    void refresh().catch((e: Error) => setError(e.message)).finally(() => setLoading(false));
    const polling = setInterval(() => void refresh().catch((e: Error) => setError(e.message)), 4000);
    const ticker = setInterval(() => setClock(Date.now()), 1000);
    return () => { clearInterval(polling); clearInterval(ticker); };
  }, [refresh]);

  useEffect(() => {
    if (applyClass) applyInput.current?.focus();
  }, [applyClass]);

  const perform = async (operation: () => Promise<void>, success: string) => {
    setBusy(true); setError(''); setNotice('');
    try {
      await operation();
      await refresh();
      setNotice(success);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Unexpected error.');
    } finally { setBusy(false); }
  };

  const onCreate = (event: FormEvent) => {
    event.preventDefault();
    void perform(async () => {
      await request('/classes', { method: 'POST', body: { name: className.trim(), totalSeats: Number(seats) } });
      setClassName('');
      setView('classes');
    }, 'Admission class created successfully.');
  };

  // Applying starts with an identity step, even if the top lookup field is filled.
  const openApply = (admissionClass: AdmissionClass) => {
    setApplyClass(admissionClass);
    setDraftStudentId(studentId);
    setApplyError('');
    setError('');
  };

  const submitApplication = async (event: FormEvent) => {
    event.preventDefault();
    if (!applyClass || busy) return;
    const enteredId = draftStudentId.trim();
    if (!validStudent(enteredId)) {
      setApplyError('Enter exactly 17 digits from the student birth registration number.');
      return;
    }
    setBusy(true);
    setApplyError('');
    setError('');
    setNotice('');
    try {
      const created = await request<CreatedHold>(`/classes/${applyClass.id}/holds`, {
        method: 'POST', studentId: enteredId
      });
      setStudentId(enteredId);
      await refresh(enteredId);
      setSelectedHold(created.holdId);
      setView('mine');
      setApplyClass(null);
      setNotice('Seat reserved. Complete the simulated payment before the hold expires.');
    } catch (e) {
      setApplyError(e instanceof Error ? e.message : 'Unable to reserve this seat.');
    } finally { setBusy(false); }
  };

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
        <div className="identityBar">
          <div className="identityCaption">
            <strong>Student identification</strong>
            <span>Optional while browsing. Enter your existing birth registration number to view applications.</span>
          </div>
          <div className="identityInput">
            <label htmlFor="student-id">Birth registration number</label>
            <input id="student-id" autoComplete="off" type="text"
              value={studentId} onChange={e => { setStudentId(normalizeStudentId(e.target.value)); setHolds([]); setSelectedHold(null); setSyncWarning(''); }}
              placeholder="17-digit number" inputMode="numeric" maxLength={17}
              aria-invalid={studentId.length > 0 && !isValid}
              aria-describedby="student-id-help" />
            <small id="student-id-help" className={studentId.length > 0 && !isValid ? 'invalid' : ''}>
              {isValid ? '✓ 17/17 digits – ready' : studentId ? `${studentId.length}/17 digits – exactly 17 required` : 'Needed when applying or viewing history'}
            </small>
          </div>
        </div>
        <section className="hero heroCompact">
          <div><div className="eyebrow">ONLINE ADMISSIONS</div><h1>Apply with confidence.</h1>
            <p>Browse open classes first, then click Apply. We'll ask for the student's birth registration number before reserving a seat.</p>
          </div>
          <div className="heroSteps"><span>01 · Browse classes</span><span>02 · Enter student ID</span><span>03 · Reserve &amp; confirm</span></div>
        </section>
        <nav className="nav" aria-label="Portal sections">
          <button className={view === 'classes' ? 'active' : ''} onClick={() => setView('classes')}>Browse classes</button>
          <button className={view === 'mine' ? 'active' : ''} onClick={() => setView('mine')}>My applications <span className="counter">{holds.length}</span></button>
          <button className={view === 'admin' ? 'active' : ''} onClick={() => setView('admin')}>School admin</button>
        </nav>
        {error && <div className="alert alertError" role="alert"><span>{error}</span><button onClick={() => setError('')} aria-label="Dismiss error">×</button></div>}
        {notice && <div className="alert alertSuccess" role="status"><span>{notice}</span><button onClick={() => setNotice('')} aria-label="Dismiss notification">×</button></div>}
        {syncWarning && <div className="alert alertWarning" role="alert"><span>{syncWarning}</span><button onClick={() => void refresh().catch((e: Error) => setError(e.message))}>Check again</button></div>}
        {view === 'classes' && <section className="section">
          <div className="sectionHeading"><div><h2>Available admission classes</h2><p>Seat counts update automatically every few seconds.</p></div><span className="countPill">{classes.length} classes</span></div>
          {loading ? <p className="muted">Loading admission classes...</p> : classes.length === 0 ? <div className="empty">No admission classes found. Open School admin to create a class. If you have an existing application, check the connection warning above.</div> :
            <div className="cards">{classes.map(c => <article className="classCard" key={c.id}>
              <div className="cardHead"><div className="schoolSymbol">▦</div><span className={c.availableSeats ? 'availability yes' : 'availability no'}>{c.availableSeats ? 'Seats available' : 'Currently full'}</span></div>
              <h3>{c.name}</h3><div className="stats"><div><strong>{c.totalSeats}</strong><span>Total</span></div><div><strong>{c.heldSeats}</strong><span>Held</span></div><div><strong>{c.confirmedSeats}</strong><span>Confirmed</span></div></div>
              <div className="bottomCard"><span><b>{c.availableSeats}</b> available</span><button disabled={busy || c.availableSeats <= 0} onClick={() => openApply(c)}>{c.availableSeats ? 'Apply now' : 'Full'}</button></div>
            </article>)}</div>}
          <div className="hint">You can browse without entering a student ID. Select Apply now to provide one and reserve a seat.</div>
        </section>}
        {view === 'mine' && <section className="section">
          <div className="sectionHeading"><div><h2>My applications</h2><p>Application history is saved in the database and remains available after refreshing.</p></div><button className="secondaryButton" onClick={() => void refresh().catch((e: Error) => setError(e.message))}>Refresh</button></div>
          {!isValid ? <div className="empty">Enter the student's exact 17-digit birth registration number in the top field to see applications.</div> : holds.length === 0 ? <div className="empty">No applications for this student yet. Browse classes to apply.</div> : <div className="holdList">{holds.map(h => {
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

      {applyClass && <div className="modalBackdrop" onMouseDown={e => { if (e.target === e.currentTarget && !busy) setApplyClass(null); }}>
        <section className="applyModal" role="dialog" aria-modal="true" aria-labelledby="apply-title" onKeyDown={e => { if (e.key === 'Escape' && !busy) setApplyClass(null); }}>
          <div className="modalHeader"><div><span className="modalEyebrow">ADMISSION · STEP 1 OF 2</span><h2 id="apply-title">Apply to {applyClass.name}</h2></div><button className="modalClose" disabled={busy} onClick={() => setApplyClass(null)} aria-label="Close application form">×</button></div>
          <p className="modalIntro">Enter the student's existing 17-digit birth registration number. This identifies the application; no separate account or registration form is required.</p>
          <form onSubmit={submitApplication} className="applyForm">
            <label htmlFor="apply-student-id">Student birth registration number</label>
            <input id="apply-student-id" ref={applyInput} type="text" autoComplete="off" inputMode="numeric" maxLength={17}
              placeholder="Enter all 17 digits" value={draftStudentId}
              onChange={e => { setDraftStudentId(normalizeStudentId(e.target.value)); setApplyError(''); }}
              aria-invalid={draftStudentId.length > 0 && !validStudent(draftStudentId)} aria-describedby="apply-id-help" />
            <small id="apply-id-help" className={draftStudentId.length > 0 && !validStudent(draftStudentId) ? 'invalid' : ''}>
              {validStudent(draftStudentId) ? '✓ Complete: 17/17 digits' : `${draftStudentId.length}/17 digits entered`}
            </small>
            {applyError && <p className="modalError" role="alert">{applyError}</p>}
            <div className="modalNote">After you reserve a seat, you'll be taken to the payment step. The payment is simulated, as required by the assignment.</div>
            <div className="modalActions"><button className="secondaryButton" type="button" disabled={busy} onClick={() => setApplyClass(null)}>Back</button><button className="primaryButton" type="submit" disabled={busy || !validStudent(draftStudentId)}>{busy ? 'Reserving...' : 'Reserve seat & continue'}</button></div>
          </form>
        </section>
      </div>}
    </div>
  );
}
