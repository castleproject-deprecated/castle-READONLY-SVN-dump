// Copyright 2004-2005 Castle Project - http://www.castleproject.org/
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

namespace Castle.Services.Security
{
	using System;
	using System.Security;
	using System.Security.Permissions;

	[Serializable, AttributeUsage(AttributeTargets.Class|AttributeTargets.Method|AttributeTargets.Property, AllowMultiple=false, Inherited=false)]
	public sealed class CustomPermissionAttribute : SecurityAttribute // CodeAccessSecurityAttribute
	{
		private readonly string permissionName;

		public CustomPermissionAttribute(SecurityAction action, String permissionName) : base(action)
		{
			this.permissionName = permissionName;
		}

		public override IPermission CreatePermission()
		{
			return new CustomPermission(permissionName);
		}
	}
}