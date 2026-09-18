import {
	BrowserRouter,
	Navigate,
	Route,
	Routes,
} from "react-router-dom";

import LoginPage from "../../features/auth/pages/LoginPage";
import ServiceRequestListPage from "../../features/serviceRequests/pages/ServiceRequestListPage";
import CreateServiceRequestPage from "../../features/serviceRequests/pages/CreateServiceRequestPage";
import ServiceRequestDetailPage from "../../features/serviceRequests/pages/ServiceRequestDetailPage";
import EditServiceRequestPage from "../../features/serviceRequests/pages/EditServiceRequestPage";

import ProtectedRoute from "../../shared/auth/ProtectedRoute";
import { useAuthStore } from "../../shared/auth/authStore";
import AdminLayout from "../../shared/layouts/AdminLayout";
import CustomerLayout from "../../shared/layouts/CustomerLayout";
import PlaceholderPage from "../../shared/components/PlaceholderPage";
import AdminDashboardPage from "../../features/admin/pages/AdminDashboardPage";
import AdminServiceRequestListPage from "../../features/admin/pages/AdminServiceRequestListPage";
import AiWorkflowsPage from "../../features/aiWorkflows/pages/AiWorkflowsPage";

function RootRedirect() {
	const user = useAuthStore((state) => state.user);
	const token = useAuthStore((state) => state.token);

	if (!token || !user) {
		return <Navigate to="/login" replace />;
	}

	if (user.role === "Admin") {
		return <Navigate to="/dashboard" replace />;
	}

	return <Navigate to="/unauthorized" replace />;
}

function AppRouter() {
	return (
		<BrowserRouter>
			<Routes>
				<Route
					path="/login"
					element={<LoginPage />}
				/>

				{/* Admin Protected Routes */}
				<Route
					element={
						<ProtectedRoute
							allowedRoles={["Admin"]}
						/>
					}
				>
					<Route element={<AdminLayout />}>
						<Route
							path="/dashboard"
							element={
								<AdminDashboardPage />
							}
						/>

						<Route
							path="/admin/service-requests"
							element={
								<AdminServiceRequestListPage />
							}
						/>

						<Route
							path="/providers"
							element={
								<PlaceholderPage title="Providers" />
							}
						/>

						<Route
							path="/quotations"
							element={
								<PlaceholderPage title="Quotations" />
							}
						/>

						<Route
							path="/service-tracking"
							element={
								<PlaceholderPage title="Service Tracking" />
							}
						/>

						<Route
							path="/ai-workflows"
							element={
								<AiWorkflowsPage />
							}
						/>
					</Route>
				</Route>

				{/* Customer Protected Routes */}
				<Route
					element={
						<ProtectedRoute
							allowedRoles={["Customer"]}
						/>
					}
				>
					<Route element={<CustomerLayout />}>
						<Route
							path="/service-requests"
							element={<ServiceRequestListPage />}
						/>

						<Route
							path="/service-requests/new"
							element={<CreateServiceRequestPage />}
						/>

						<Route
							path="/service-requests/:id"
							element={<ServiceRequestDetailPage />}
						/>

						<Route
							path="/service-requests/:id/edit"
							element={<EditServiceRequestPage />}
						/>
					</Route>
				</Route>

				<Route
					path="/unauthorized"
					element={<PlaceholderPage title="Unauthorized" />}
				/>

				<Route
					path="/"
					element={<RootRedirect />}
				/>

				<Route
					path="*"
					element={<PlaceholderPage title="Page Not Found" />}
				/>
			</Routes>
		</BrowserRouter>
	);
}

export default AppRouter;
