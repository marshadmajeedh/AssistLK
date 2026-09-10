import React, { useEffect, useMemo, useRef, useState } from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter, Navigate, Route, Routes, useNavigate } from 'react-router-dom';
import { motion, AnimatePresence, useInView } from 'framer-motion';
import { ArrowRight, Check, ChevronLeft, ChevronRight, Menu, Play, Sparkles, X, Zap } from 'lucide-react';
import { AreaChart, Area, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { authApi } from './shared/api/client';
import UserManagementPage from './features/users/UserManagementPage';
import './styles.css';
import './auth.css';

const logos = ['Vehicle Assist', 'Plumbing', 'Electrical', 'Appliance Care', 'Verified Pros', 'AssistLK'];
const heroScenes = [
  {eyebrow:'ASSISTLK PLATFORM', title:'Intelligent coordination for urgent skilled services', note:'Report problems, match verified providers, and track every service journey.', tone:'scene-a'},
  {eyebrow:'AI-ASSISTED OPERATIONS', title:'Move from customer request to resolution with confidence', note:'Structured workflows keep customers, providers, and staff aligned.', tone:'scene-b'},
  {eyebrow:'CONTROLLED SERVICE WORKFLOWS', title:'Bring every service decision into one trusted workspace', note:'Human approvals, validation, and audit history keep the platform accountable.', tone:'scene-c'}
];
const reviews = [
  {tag:'A customer requesting help', quote:'“I could explain the problem once and follow the service from request to completion.”', name:'Nimali P.', role:'AssistLK customer'},
  {tag:'A verified service provider', quote:'“The request details and location help me respond to the right jobs faster.”', name:'Kasun R.', role:'Verified provider'},
  {tag:'An authorized staff member', quote:'“Every recommendation and approval is visible in one auditable workflow.”', name:'Dilan S.', role:'Platform administrator'}
];
const products = [
  {title:'Service request management', body:'Review descriptions, images, urgency, location, classification, and status history.', tone:'dispatch'},
  {title:'Provider matching and verification', body:'Check skills, availability, service area, verification status, and match reasons.', tone:'profit'},
  {title:'Quotation and service coordination', body:'Track provider offers, customer approvals, assignments, and controlled next steps.', tone:'customer'}
];

function Reveal({children, delay=0, className=''}) {
  return <motion.div className={className} initial={{opacity:0,y:24}} whileInView={{opacity:1,y:0}} viewport={{once:true,amount:.22}} transition={{duration:.65,ease:[.22,1,.36,1],delay}}>{children}</motion.div>;
}
function Logo(){ return <div className="logo"><span className="logo-mark"><Sparkles size={13}/></span><span>AssistLK</span></div> }
function Navbar(){
  const [open,setOpen]=useState(false);
  return <>
    <div className="announcement"><span>New: AI-assisted service coordination is live</span><ArrowRight size={14}/></div>
    <header className="nav-wrap">
      <nav className="nav">
        <Logo/>
        <div className="nav-links">
          <a href="#platform">Dashboard</a><a href="#solutions">Workflows</a><a href="/users">Users</a><a href="#resources">Resources</a>
        </div>
        <div className="nav-actions"><a className="login" href="/login">Log in</a><a className="btn btn-primary small" href="/login">Get a demo</a></div>
        <button className="mobile-menu" onClick={()=>setOpen(!open)} aria-label="Toggle menu">{open?<X/>:<Menu/>}</button>
      </nav>
      <AnimatePresence>{open && <motion.div className="mobile-panel" initial={{height:0,opacity:0}} animate={{height:'auto',opacity:1}} exit={{height:0,opacity:0}}><a href="#platform">Dashboard</a><a href="#solutions">Workflows</a><a href="/users">Users</a><a href="#resources">Resources</a><button className="btn btn-primary">Explore dashboard</button></motion.div>}</AnimatePresence>
    </header>
  </>
}
function Hero(){
  const [scene,setScene]=useState(0);
  useEffect(()=>{ const id=setInterval(()=>setScene(s=>(s+1)%heroScenes.length),5200); return()=>clearInterval(id);},[]);
  const data=heroScenes[scene];
  return <section className="hero">
    <div className="hero-backdrop"><AnimatePresence mode="wait"><motion.div key={data.tone} className={`hero-scene ${data.tone}`} initial={{opacity:0,scale:1.04}} animate={{opacity:1,scale:1}} exit={{opacity:0,scale:1.01}} transition={{duration:1.1}}><div className="scene-glow"/><div className="scene-grid"/><div className="scene-person"><div className="helmet"/><div className="head"/><div className="body"/><div className="tablet"><span/></div></div><div className="scene-orb orb-1"/><div className="scene-orb orb-2"/></motion.div></AnimatePresence></div>
    <div className="container hero-inner">
      <motion.div key={scene} className="hero-copy" initial={{opacity:0,y:14}} animate={{opacity:1,y:0}} transition={{duration:.6}}>
        <div className="eyebrow">{data.eyebrow}<span className="eyebrow-dot"/></div>
        <h1>{data.title}</h1>
        <p>{data.note}</p>
        <div className="hero-form"><input placeholder="Work email"/><button className="btn btn-primary">Explore AssistLK <ArrowRight size={16}/></button></div>
        <div className="rating-row"><span><strong>4.8</strong> / 5 service experience</span><span className="tiny-rule"/><span>Built for customers, providers, and staff</span></div>
      </motion.div>
    </div>
    <div className="scene-pips">{heroScenes.map((_,i)=><button key={i} className={i===scene?'active':''} onClick={()=>setScene(i)} aria-label={`Scene ${i+1}`}/>)}</div>
  </section>
}
function LogoStrip(){ return <section className="logo-strip"><div className="container"><div className="logo-caption">One workflow for every service category</div><div className="logos">{logos.map((l,i)=><div className="partner" key={i}><span className="partner-badge">✦</span>{l}</div>)}</div></div></section> }
function VideoBlock(){
  return <Reveal><section className="container video-section">
    <div className="video-frame"><div className="video-bg"><div className="video-ui"><div className="video-top"><span/><span/><span/></div><div className="video-bars"><div/><div/><div/><div/></div><div className="video-chart"/></div><button className="play"><Play fill="currentColor" size={23}/></button></div></div>
    <div className="video-copy"><div className="eyebrow">BUILT FOR REAL SERVICE PROBLEMS</div><h2>Make urgent help easier to request, coordinate, and complete.</h2><p>AssistLK connects customer reports, verified providers, quotations, approvals, tracking, feedback, and audit history in one platform.</p><a className="text-link" href="#platform">Explore the staff dashboard <ArrowRight size={16}/></a></div>
  </section></Reveal>
}
function SolutionCards(){return <section id="solutions" className="container solutions"><Reveal><div className="section-heading"><div><div className="eyebrow">ONE PLATFORM</div><h2>Everything needed to coordinate skilled service.</h2></div><p>Give customers, providers, and authorized staff the same source of truth without adding more complexity.</p></div></Reveal><div className="solution-grid">{products.map((p,i)=><Reveal key={p.title} delay={i*.08}><article className="solution-card"><div className={`solution-art ${p.tone}`}><div className="mini-window"><div className="mini-sidebar"/><div className="mini-main"><div className="mini-title"/><div className="mini-lines"><i/><i/><i/></div><div className="mini-chart"><span/><span/><span/><span/><span/></div></div></div></div><div className="solution-content"><h3>{p.title}</h3><p>{p.body}</p><a href="#platform" className="text-link">Learn more <ArrowRight size={16}/></a></div></article></Reveal>)}</div></section>}
const chartData = [{m:'Jan',v:32},{m:'Feb',v:41},{m:'Mar',v:38},{m:'Apr',v:52},{m:'May',v:61},{m:'Jun',v:68},{m:'Jul',v:76},{m:'Aug',v:84}];
function DashboardShowcase({role=JSON.parse(localStorage.getItem('assistlk.user')||'null')?.role||'Administrator'}){
  const [tab,setTab]=useState(0);
  const isStaff=role==='AuthorizedStaff';
  const cards=[
    {label:'Open requests',value:'48',delta:'+18.7%',kind:'revenue'},
    {label:'Verified providers',value:'126',delta:'+12.4%',kind:'jobs'},
    {label:'Pending approvals',value:'7',delta:'3 urgent',kind:'margin'}
  ];
  return <section id="platform" className="dashboard-section"><div className="container">
    <Reveal><div className="section-heading dashboard-heading"><div><div className="eyebrow">ASSISTLK OPERATIONS</div><h2>Coordinate every service request.</h2></div><p>Monitor requests, providers, AI recommendations, approvals, and service outcomes from one staff workspace.</p></div></Reveal>
    <div className="dashboard-shell"><div className="dash-top"><div className="dash-brand"><Logo/></div><div className="dash-breadcrumb">Operations <span>/</span> Today</div><div className="dash-actions"><span className="live-dot"/> Live workflow <button className="avatar">A</button></div></div>
      <div className="dash-body"><aside className="dash-side"><div className="side-label">WORKSPACE</div>{['Overview','Requests','Providers','Approvals','Reports'].map((x,i)=><button className={i===tab?'selected':''} onClick={()=>setTab(i)} key={x}><span className="side-icon"/>{x}</button>)}<div className="side-spacer"/><div className="side-label">SYSTEM</div><button><span className="side-icon"/>Audit log</button></aside>
      <main className="dash-main"><div className="dash-title-row"><div><div className="muted">Monday, September 9</div><h3>Good morning, {isStaff?'Staff':'Admin'}</h3></div><button className="btn btn-dark">{isStaff?'Review approvals':'Review requests'} <ArrowRight size={15}/></button></div>
        <div className="metrics">{cards.map((c,i)=><motion.div layout key={c.label} className="metric"><div className="metric-label">{c.label}</div><div className="metric-value">{c.value}</div><div className="metric-delta"><span>{c.delta}</span> vs last month</div>{c.kind==='revenue'&&<div className="spark"><span/><span/><span/><span/><span/><span/></div>}{c.kind==='jobs'&&<div className="progress"><i style={{width:'72%'}}/></div>}{c.kind==='margin'&&<div className="ring"><span>+4.2</span></div>}</motion.div>)}</div>
        <div className="dash-grid"><div className="panel large"><div className="panel-head"><div><b>Service request volume</b><span>Rolling 8 months</span></div><button>Last 8 months ▾</button></div><div className="chart-wrap"><ResponsiveContainer width="100%" height={230}><AreaChart data={chartData}><defs><linearGradient id="fill" x1="0" y1="0" x2="0" y2="1"><stop offset="0%" stopOpacity={0.22}/><stop offset="100%" stopOpacity={0.02}/></linearGradient></defs><XAxis dataKey="m" axisLine={false} tickLine={false} tick={{fontSize:11}}/><YAxis hide/><Tooltip/><Area type="monotone" dataKey="v" stroke="currentColor" fill="url(#fill)" strokeWidth={3} dot={false}/></AreaChart></ResponsiveContainer></div></div>
        <div className="panel"><div className="panel-head"><div><b>Active requests</b><span>Live workflow</span></div><button>View all</button></div><div className="job-list">{[['SR-2048','Water leak · Colombo 05','AI review'],['SR-2047','Vehicle breakdown · Kandy','Provider matched'],['SR-2046','Power fault · Galle','Awaiting quote'],['SR-2045','AC repair · Negombo','In progress']].map((j,i)=><div className="job" key={j[0]}><div className="job-avatar">{i+1}</div><div className="job-copy"><b>{j[0]}</b><span>{j[1]}</span></div><span className={`status s${i}`}>{j[2]}</span></div>)}</div></div></div>
      </main></div>
    </div>
  </div></section>
}
const adminModules = [
  ['Dashboard', 'Overview and live operational metrics', '#platform'],
  ['Service Requests', 'Review submitted problems and status history', '#requests'],
  ['Providers', 'Verification, availability, and matching', '#providers'],
  ['AI Workflows', 'Agent steps, validation, and safe failures', '#ai-workflows'],
  ['Approvals', 'Review high-impact recommendations', '#approvals'],
  ['Complaints', 'Track issues and resolutions', '#complaints'],
  ['Reports', 'Service performance and activity', '#reports'],
  ['Users', 'Manage roles and account access', '/users'],
  ['Audit Log', 'Trace important actions and decisions', '#audit-log']
];
const staffModules = adminModules.filter(([label]) => label !== 'Users');
function AdminDashboard(){
  const [active,setActive]=useState('Dashboard');
  const selected=adminModules.find(([label])=>label===active) || adminModules[0];
  return <main className="admin-app"><header className="admin-topbar"><Logo/><div className="admin-crumb">Admin workspace <span>/</span> {selected[0]}</div><div className="admin-top-actions"><span className="live-dot"/> Connected <button className="avatar">A</button></div></header><div className="admin-layout"><aside className="admin-sidebar"><div className="side-label">OPERATIONS</div>{adminModules.map(([label,,href])=><a key={label} href={href} className={active===label?'selected':''} onClick={()=>setActive(label)}><span className="side-icon"/>{label}</a>)}<div className="side-spacer"/><div className="side-label">ACCOUNT</div><a href="/users"><span className="side-icon"/>Profile and access</a><a href="/login"><span className="side-icon"/>Sign out</a></aside><section className="admin-content"><div className="admin-heading"><div><div className="eyebrow">ASSISTLK ADMINISTRATION</div><h1>{selected[0]}</h1><p>{selected[1]}</p></div>{active==='Dashboard'&&<button className="btn btn-dark">Review priority items <ArrowRight size={15}/></button>}</div>{active==='Dashboard'?<DashboardShowcase/>:<section className="module-placeholder"><div className="placeholder-icon"><Sparkles size={22}/></div><div><div className="eyebrow">MODULE FOUNDATION</div><h2>{selected[0]} is ready for implementation.</h2><p>This page is the shared structural entry point for the assigned team component. Connect its ASP.NET Core endpoints, filters, loading states, and business actions here.</p><div className="placeholder-meta"><span>Owner boundary</span><b>{selected[0]==='Service Requests'?'Member 1':selected[0]==='Providers'?'Member 2':selected[0]==='Approvals'?'Member 3':selected[0]==='Complaints'?'Member 4':'Shared staff foundation'}</b></div></div></section>}</section></div></main>;
}
function AuthorizedStaffDashboard(){
  const [active,setActive]=useState('Dashboard');
  const selected=staffModules.find(([label])=>label===active) || staffModules[0];
  return <main className="admin-app"><header className="admin-topbar"><Logo/><div className="admin-crumb">Staff workspace <span>/</span> {selected[0]}</div><div className="admin-top-actions"><span className="live-dot"/> Connected <button className="avatar">S</button></div></header><div className="admin-layout"><aside className="admin-sidebar"><div className="side-label">OPERATIONS</div>{staffModules.map(([label,,href])=><a key={label} href={href} className={active===label?'selected':''} onClick={()=>setActive(label)}><span className="side-icon"/>{label}</a>)}<div className="side-spacer"/><div className="side-label">ACCOUNT</div><a href="/login"><span className="side-icon"/>Sign out</a></aside><section className="admin-content"><div className="admin-heading"><div><div className="eyebrow">ASSISTLK AUTHORIZED STAFF</div><h1>{selected[0]}</h1><p>{selected[1]}</p></div>{active==='Dashboard'&&<button className="btn btn-dark">Review priority items <ArrowRight size={15}/></button>}</div>{active==='Dashboard'?<DashboardShowcase/>:<section className="module-placeholder"><div className="placeholder-icon"><Sparkles size={22}/></div><div><div className="eyebrow">STAFF MODULE FOUNDATION</div><h2>{selected[0]} is ready for implementation.</h2><p>This staff workspace is limited to operational review, approvals, workflow monitoring, complaints, reports, and audit visibility. Administrator-only user management is not available here.</p><div className="placeholder-meta"><span>Access role</span><b>Authorized Staff</b></div></div></section>}</section></div></main>;
}
function Reviews(){return <section id="proof" className="container reviews"><Reveal><div className="center-heading"><div className="eyebrow">DESIGNED AROUND REAL ROLES</div><h2>One connected experience from report to resolution.</h2><p>AssistLK keeps every decision clear for the customer, provider, and authorized staff member.</p></div></Reveal><div className="review-grid">{reviews.map((r,i)=><Reveal delay={i*.08} key={r.name}><article className="review-card"><div className="review-media"><div className="person-shape"><div className="face"/><div className="shirt"/></div><button className="play small"><Play fill="currentColor" size={17}/></button></div><div className="review-copy"><div className="review-tag">{r.tag}</div><blockquote>{r.quote}</blockquote><div className="review-person"><b>{r.name}</b><span>{r.role}</span></div></div></article></Reveal>)}</div></section>}
function Integrations(){ const items=['Maps & GPS','ASP.NET Core','PostgreSQL','AI Agents','JWT Identity','Audit Events']; return <section className="integration"><div className="container integration-inner"><Reveal><div className="integration-copy"><div className="eyebrow">CONNECTED SYSTEM ARCHITECTURE</div><h2>Keep every service decision connected and traceable.</h2><p>React and Flutter share one API, database, identity layer, business rules, and Agentic AI workflow.</p><a className="text-link" href="#resources">View project resources <ArrowRight size={16}/></a></div></Reveal><div className="network"><div className="network-core"><Zap size={28}/><span>AssistLK</span></div>{items.map((x,i)=><motion.div key={x} className={`node n${i}`} initial={{opacity:0,scale:.7}} whileInView={{opacity:1,scale:1}} viewport={{once:true,amount:.35}} transition={{duration:.45,delay:.08*i}}><span className="node-dot"/>{x}</motion.div>)}<svg className="network-lines" viewBox="0 0 560 360" preserveAspectRatio="none">{items.map((_,i)=>{const a=[[75,50],[83,23],[85,72],[10,22],[10,76],[40,4]][i];return <line key={i} x1="50%" y1="50%" x2={`${a[0]}%`} y2={`${a[1]}%`} />})}</svg></div></div></section>}
function CTA(){return <section className="cta"><div className="container cta-inner"><Reveal><div><div className="eyebrow">READY TO COORDINATE</div><h2>Give every service request a clear next step.</h2><p>Bring AI recommendations, human approvals, provider coordination, tracking, complaints, and reporting into one staff workspace.</p><div className="cta-actions"><button className="btn btn-primary">Explore dashboard <ArrowRight size={16}/></button><button className="btn btn-ghost">View workflows</button></div></div></Reveal></div></section>}
function Footer(){return <footer id="resources" className="footer"><div className="container"><div className="footer-top"><Logo/><div className="footer-tag">AI-assisted service coordination for customers, providers, and staff.</div></div><div className="footer-grid">{[['Platform','Dashboard','Service Requests','Providers','AI Workflows'],['Coordination','Approvals','Quotations','Tracking','Complaints'],['Resources','API Standards','Testing','Deployment','Project Guide'],['Roles','Customer','Service Provider','Administrator','Authorized Staff']].map(([h,...links])=><div key={h}><b>{h}</b>{links.map(l=><a href="#" key={l}>{l}</a>)}</div>)}<div className="newsletter"><b>Get project updates</b><div className="newsletter-row"><input placeholder="Email address"/><button className="btn btn-primary small">Subscribe</button></div></div></div><div className="footer-bottom"><span>© 2026 AssistLK</span><span>Privacy · Terms · Auditability</span></div></div></footer>}
function LoginScreen(){
  const navigate=useNavigate();
  const [mode,setMode]=useState('login');
  const [form,setForm]=useState({email:'admin@assistlk.local',password:'ChangeMe123!',displayName:''});
  const [error,setError]=useState('');
  const [busy,setBusy]=useState(false);
  const isRegister=mode==='register';
  async function submit(event){
    event.preventDefault();
    setBusy(true); setError('');
    try{
      const result=isRegister?await authApi.register(form):await authApi.login({email:form.email,password:form.password});
      localStorage.setItem('assistlk.accessToken',result.accessToken);
      localStorage.setItem('assistlk.user',JSON.stringify(result));
      navigate(result.role==='AuthorizedStaff'?'/staff':'/dashboard');
    }catch(requestError){setError(requestError.message);}
    finally{setBusy(false);}
  }
  return <main className="auth-page"><div className="auth-art"><div className="auth-grid"/><div className="auth-orbit orbit-a"/><div className="auth-orbit orbit-b"/><div className="auth-art-copy"><div className="eyebrow">ASSISTLK OPERATIONS</div><h1>One clear next step for every service request.</h1><p>Coordinate customers, verified providers, AI recommendations, and approvals from one trusted workspace.</p></div></div><section className="auth-panel"><Logo/><div className="auth-panel-copy"><div className="eyebrow">{isRegister?'CREATE ACCOUNT':'STAFF WORKSPACE'}</div><h2>{isRegister?'Join AssistLK':'Welcome back'}</h2><p>{isRegister?'Create a customer or provider account to start the shared workflow.':'Sign in to monitor service requests and operational workflows.'}</p></div><form onSubmit={submit} className="auth-form">{isRegister&&<label>Display name<input required value={form.displayName} onChange={event=>setForm({...form,displayName:event.target.value})} placeholder="Your name"/></label>}<label>Email<input required type="email" value={form.email} onChange={event=>setForm({...form,email:event.target.value})} placeholder="you@example.com"/></label><label>Password<input required minLength={8} type="password" value={form.password} onChange={event=>setForm({...form,password:event.target.value})} placeholder="At least 8 characters"/></label>{error&&<div className="auth-error" role="alert">{error}</div>}<button className="btn btn-primary auth-submit" disabled={busy}>{busy?'Connecting...':isRegister?'Create account':'Sign in'} <ArrowRight size={16}/></button></form><button className="auth-switch" onClick={()=>{setMode(isRegister?'login':'register');setError('')}}>{isRegister?'Already have an account? Sign in':'Need an account? Create one'}</button><div className="auth-note">Development account: admin@assistlk.local / ChangeMe123!</div></section></main>
}
function ProtectedRoute(){
  const user=JSON.parse(localStorage.getItem('assistlk.user')||'null');
  return localStorage.getItem('assistlk.accessToken')&&user?.role==='Administrator'?<AdminDashboard/>:<Navigate to="/login" replace/>;
}
function StaffRoute(){
  const user=JSON.parse(localStorage.getItem('assistlk.user')||'null');
  return localStorage.getItem('assistlk.accessToken')&&user?.role==='AuthorizedStaff'?<AuthorizedStaffDashboard/>:<Navigate to="/login" replace/>;
}
function App(){return <><Navbar/><Hero/><LogoStrip/><VideoBlock/><SolutionCards/><DashboardShowcase/><Reviews/><Integrations/><CTA/><Footer/></>}

createRoot(document.getElementById('root')).render(
  <BrowserRouter>
    <Routes>
      <Route path="/" element={<App/>}/>
      <Route path="/preview" element={<App/>}/>
      <Route path="/login" element={<LoginScreen/>}/>
      <Route path="/dashboard" element={<ProtectedRoute/>}/>
      <Route path="/staff" element={<StaffRoute/>}/>
      <Route path="/users" element={localStorage.getItem('assistlk.accessToken')?<UserManagementPage/>:<Navigate to="/login" replace/>}/>
      <Route path="*" element={<Navigate to="/" replace/>}/>
    </Routes>
  </BrowserRouter>
);
