import React, { useState } from 'react';
import { ArrowRight, Sparkles, UserPlus, Shield, CheckCircle, XCircle } from 'lucide-react';

const mockUsers = [
  { id: '1', fullName: 'Admin User', email: 'admin@assistlk.local', role: 'Administrator', isActive: true },
  { id: '2', fullName: 'Dilan Silva', email: 'dilan@assistlk.local', role: 'AuthorizedStaff', isActive: true },
  { id: '3', fullName: 'Kasun Rajapaksha', email: 'kasun@provider.lk', role: 'ServiceProvider', isActive: true },
  { id: '4', fullName: 'Nimali Perera', email: 'nimali@customer.lk', role: 'Customer', isActive: true },
];

export default function UserManagementPage() {
  const [users, setUsers] = useState(mockUsers);
  const [searchTerm, setSearchTerm] = useState('');

  const filteredUsers = users.filter(u => 
    u.fullName.toLowerCase().includes(searchTerm.toLowerCase()) || 
    u.email.toLowerCase().includes(searchTerm.toLowerCase()) ||
    u.role.toLowerCase().includes(searchTerm.toLowerCase())
  );

  return (
    <main className="admin-app">
      <header className="admin-topbar">
        <div className="logo"><span className="logo-mark"><Sparkles size={13}/></span><span>AssistLK</span></div>
        <div className="admin-crumb">Admin workspace <span>/</span> User Management</div>
        <div className="admin-top-actions">
          <span className="live-dot"/> Connected
          <button className="avatar">A</button>
        </div>
      </header>
      
      <div className="admin-layout">
        <aside className="admin-sidebar">
          <div className="side-label">OPERATIONS</div>
          <a href="/dashboard"><span className="side-icon"/>Dashboard</a>
          <a href="/users" className="selected"><span className="side-icon"/>User Management</a>
          <div className="side-spacer"/>
          <div className="side-label">ACCOUNT</div>
          <a href="/login"><span className="side-icon"/>Sign out</a>
        </aside>

        <section className="admin-content">
          <div className="admin-heading">
            <div>
              <div className="eyebrow">USER ACCESS MANAGEMENT</div>
              <h1>Platform Users & Roles</h1>
              <p>Manage system access, assign roles, and activate or deactivate user accounts.</p>
            </div>
            <button className="btn btn-dark">
              <UserPlus size={16}/> Add New User
            </button>
          </div>

          <div style={{ marginBottom: '20px' }}>
            <input 
              type="text" 
              placeholder="Search users by name, email or role..." 
              value={searchTerm}
              onChange={e => setSearchTerm(e.target.value)}
              style={{
                width: '100%',
                padding: '12px 16px',
                borderRadius: '8px',
                border: '1px solid #e7e7ea',
                outline: 'none',
                fontSize: '14px'
              }}
            />
          </div>

          <div className="dashboard-shell" style={{ overflow: 'hidden' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '13px' }}>
              <thead>
                <tr style={{ background: '#f5f5f6', borderBottom: '1px solid #ececef' }}>
                  <th style={{ padding: '12px 16px' }}>User</th>
                  <th style={{ padding: '12px 16px' }}>Email</th>
                  <th style={{ padding: '12px 16px' }}>Role</th>
                  <th style={{ padding: '12px 16px' }}>Status</th>
                  <th style={{ padding: '12px 16px' }}>Action</th>
                </tr>
              </thead>
              <tbody>
                {filteredUsers.map(user => (
                  <tr key={user.id} style={{ borderBottom: '1px solid #f0f0f2' }}>
                    <td style={{ padding: '14px 16px', fontWeight: '600' }}>{user.fullName}</td>
                    <td style={{ padding: '14px 16px', color: '#616269' }}>{user.email}</td>
                    <td style={{ padding: '14px 16px' }}>
                      <span className="status s2" style={{ fontWeight: '600' }}>{user.role}</span>
                    </td>
                    <td style={{ padding: '14px 16px' }}>
                      {user.isActive ? (
                        <span style={{ color: '#27824d', display: 'inline-flex', alignItems: 'center', gap: '4px' }}>
                          <CheckCircle size={14}/> Active
                        </span>
                      ) : (
                        <span style={{ color: '#b9363b', display: 'inline-flex', alignItems: 'center', gap: '4px' }}>
                          <XCircle size={14}/> Inactive
                        </span>
                      )}
                    </td>
                    <td style={{ padding: '14px 16px' }}>
                      <button className="btn btn-ghost small">Edit Role</button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      </div>
    </main>
  );
}
