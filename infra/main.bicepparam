using './main.bicep'

param location = 'newzealandnorth'
param prefix = 'nznewsagg'
param postgresAdminPassword = ''
param postgresAdminUsername = 'pgadminnznewsagg'
param postgresDbName = 'nznewsagg-db'
param appServicePlanSku = 'B1'
param analyzerProvider = 'azure'
param languageSku = 'S'
param huggingFaceToken = ''
param apiSecurityKey = ''

