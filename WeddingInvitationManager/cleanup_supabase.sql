-- Clean up existing partially applied tables
-- Run this in Supabase SQL Editor

-- Drop tables in reverse order to handle foreign key constraints
DROP TABLE IF EXISTS "QRScans" CASCADE;
DROP TABLE IF EXISTS "InvitationTemplates" CASCADE;
DROP TABLE IF EXISTS "Invitations" CASCADE;
DROP TABLE IF EXISTS "Contacts" CASCADE;
DROP TABLE IF EXISTS "Events" CASCADE;
DROP TABLE IF EXISTS "AspNetUserTokens" CASCADE;
DROP TABLE IF EXISTS "AspNetUserRoles" CASCADE;
DROP TABLE IF EXISTS "AspNetUserLogins" CASCADE;
DROP TABLE IF EXISTS "AspNetUserClaims" CASCADE;
DROP TABLE IF EXISTS "AspNetRoleClaims" CASCADE;
DROP TABLE IF EXISTS "AspNetUsers" CASCADE;
DROP TABLE IF EXISTS "AspNetRoles" CASCADE;
DROP TABLE IF EXISTS "__EFMigrationsHistory" CASCADE;
